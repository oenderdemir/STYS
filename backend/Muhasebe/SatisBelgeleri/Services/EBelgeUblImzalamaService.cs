using System.Collections.Immutable;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>Bir e-belgeyi imzalama talebini taşır - claim'den gelen lease bilgisiyle AYNI sözleşme (bkz. Faz 2B.6.1/2B.6.2 EBelgeArtefaktOlusturmaTalebi ile AYNI desen).</summary>
public sealed record EBelgeUblImzalamaTalebi(
    int KurumId,
    int EBelgeKaydiId,
    int OutboxMesajiId,
    string KilitToken,
    DateTime KilitBitisZamaniUtc);

public enum EBelgeUblImzalamaSonucuTuru
{
    AtomikBasarili = 1,
    GeciciHata = 2,
    AtomikKaliciHata = 3,
    SahiplikKaybedildi = 4
}

public sealed record EBelgeUblImzalamaSonucu
{
    public EBelgeUblImzalamaSonucuTuru SonucTuru { get; }

    public string? HataKodu { get; }

    public string? HataMesaji { get; }

    public bool BasariliMi => SonucTuru == EBelgeUblImzalamaSonucuTuru.AtomikBasarili;

    private EBelgeUblImzalamaSonucu(EBelgeUblImzalamaSonucuTuru sonucTuru, string? hataKodu, string? hataMesaji)
    {
        SonucTuru = sonucTuru;
        HataKodu = hataKodu;
        HataMesaji = hataMesaji;
    }

    public static EBelgeUblImzalamaSonucu AtomikBasarili() => new(EBelgeUblImzalamaSonucuTuru.AtomikBasarili, null, null);

    public static EBelgeUblImzalamaSonucu GeciciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeUblImzalamaSonucuTuru.GeciciHata, hataKodu, hataMesaji);
    }

    public static EBelgeUblImzalamaSonucu AtomikKaliciHata(string hataKodu, string hataMesaji)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(hataKodu, hataMesaji);
        return new(EBelgeUblImzalamaSonucuTuru.AtomikKaliciHata, hataKodu, hataMesaji);
    }

    public static EBelgeUblImzalamaSonucu SahiplikKaybedildi() => new(EBelgeUblImzalamaSonucuTuru.SahiplikKaybedildi, null, null);
}

public interface IEBelgeUblImzalamaService
{
    Task<EBelgeUblImzalamaSonucu?> ImzalaAsync(EBelgeUblImzalamaTalebi talep, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IEBelgeUblImzalamaService"/>'in GERÇEK implementasyonu - immutable Unsigned UBL
/// artifact'ını GERÇEK <see cref="IEBelgeXmlImzalayici"/> ile XAdES-BES imzalar, imzayı bağımsız
/// doğrular, tam XSD + gerçek Schematron doğrulamasından geçirir ve SignedReady artefaktı
/// immutable olarak kalıcılaştırır (bkz. Faz 2B.7). Faz 2B.6.1/2B.6.2'deki AYNI atomik-transaction
/// + lease-ownership desenini (iş-türü-farkında `IsOwnedForJobAsync`/`TryCompleteJobAsync`/
/// `TryFailJobAsync`, `EBelgeOutboxIsTuru.UblImzala` ile) yeniden kullanır - genel outbox
/// mimarisi BAŞTAN YAZILMAZ (bkz. görev md.16-17).
///
/// Kesin akış: claim → DB DIŞI imzalama+bağımsız doğrulama+XSD+Schematron (satır kilidi
/// TUTULMAZ) → lease YENİDEN doğrulama → SignedReady artefakt + EBelgeKaydi + outbox TEK
/// atomik transaction (bkz. görev md.17).
/// </summary>
public sealed class EBelgeUblImzalamaService : IEBelgeUblImzalamaService
{
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeXmlImzalayici _imzalayici;
    private readonly IEBelgeXmlImzaDogrulayici _dogrulayici;
    private readonly IEBelgeUblXsdValidator _xsdValidator;
    private readonly IEBelgeSchematronValidator _schematronValidator;
    private readonly IEBelgeOutboxLeaseTransitionService _leaseTransitionService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EBelgeUblImzalamaService> _logger;

    public EBelgeUblImzalamaService(
        StysAppDbContext dbContext,
        IEBelgeXmlImzalayici imzalayici,
        IEBelgeXmlImzaDogrulayici dogrulayici,
        IEBelgeUblXsdValidator xsdValidator,
        IEBelgeSchematronValidator schematronValidator,
        IEBelgeOutboxLeaseTransitionService leaseTransitionService,
        TimeProvider timeProvider,
        ILogger<EBelgeUblImzalamaService> logger)
    {
        _dbContext = dbContext;
        _imzalayici = imzalayici;
        _dogrulayici = dogrulayici;
        _xsdValidator = xsdValidator;
        _schematronValidator = schematronValidator;
        _leaseTransitionService = leaseTransitionService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<EBelgeUblImzalamaSonucu?> ImzalaAsync(EBelgeUblImzalamaTalebi talep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(talep);
        talep = ValidateTalepAndNormalize(talep);

        // ---- Faz 1: kısa okuma (açık transaction yok) ----
        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .FirstOrDefaultAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        if (kayit is null)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: false, "EBELGE_SIGNING_KAYDI_BULUNAMADI", "İmzalanacak e-belge kaydı bulunamadı.", cancellationToken);
        }

        // Soft-delete edilmiş kaynak dahil GÖREBİLMEK için IgnoreQueryFilters (bkz. görev md.17
        // adım 5-6, "Artifact IgnoreQueryFilters() ile bütünlük açısından kontrol edilmeli").
        var unsignedArtifact = await _dbContext.Set<EBelgeArtifact>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.KurumId == talep.KurumId &&
                a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned,
                cancellationToken);

        if (unsignedArtifact is null)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, EBelgeXmlImzaHataKodlari.UnsignedArtifactBulunamadi, "İmzalanacak Unsigned UBL artefaktı bulunamadı.", cancellationToken);
        }

        if (unsignedArtifact.IsDeleted)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, EBelgeXmlImzaHataKodlari.UnsignedArtifactSoftDeleted, "İmzalanacak Unsigned UBL artefaktı soft-delete edilmiş.", cancellationToken);
        }

        // md.7/md.17 adım 7: unsigned bytes hash'i tekrar doğrulanmalı.
        var yenidenHesaplananHash = Convert.ToHexString(SHA256.HashData(unsignedArtifact.Icerik));
        if (!string.Equals(yenidenHesaplananHash, unsignedArtifact.ArtifactSha256, StringComparison.Ordinal))
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz, "Unsigned artefaktın kayıtlı hash'i, saklanan içerikle eşleşmiyor.", cancellationToken);
        }

        // ---- Faz 2: DB dışı imzalama + doğrulama (satır kilidi TUTULMAZ, md.17 adım 8) ----
        EBelgeXmlImzaSonucu imzaSonucu;
        try
        {
            imzaSonucu = await _imzalayici.ImzalaAsync(
                new EBelgeXmlImzaTalebi
                {
                    KurumId = talep.KurumId,
                    UnsignedUblUtf8 = ImmutableArray.Create(unsignedArtifact.Icerik),
                    UnsignedUblSha256 = unsignedArtifact.ArtifactSha256,
                    RuleSetId = unsignedArtifact.RuleSetId,
                    EBelgeUuid = kayit.EBelgeUuid,
                    ImzalamaZamaniUtc = _timeProvider.GetUtcNow().UtcDateTime,
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EBelgeSigningProviderNotConfiguredException ex)
        {
            // Konfigürasyon hatası - fail-closed, retry ANLAMSIZDIR (bkz. görev md.21 "Konfigürasyon hataları").
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, ex.HataKodu, ex.Message, cancellationToken);
        }
        catch (EBelgeXmlImzaKaliciHataException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, ex.HataKodu, ex.Message, cancellationToken);
        }
        catch (EBelgeXmlImzaGeciciHataException ex)
        {
            // EBelgeKaydi'yi hiç DEĞİŞTİRMEDİĞİNDEN atomik transaction/lease sahiplik doğrulaması
            // GEREKMEZ (bkz. Faz 2B.6.1 görev md.5 ile AYNI tasarım kararı).
            return EBelgeUblImzalamaSonucu.GeciciHata(ex.HataKodu, ex.Message);
        }

        // md.15: sonuç hash'i bağımsız yeniden hesapla - imza motorunun beyanına KÖRÜ KÖRÜNE güvenilmez.
        var signedBytes = imzaSonucu.SignedUblUtf8.ToArray();
        var signedHashGercek = Convert.ToHexString(SHA256.HashData(signedBytes));
        if (!string.Equals(signedHashGercek, imzaSonucu.SignedUblSha256, StringComparison.Ordinal))
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, EBelgeXmlImzaHataKodlari.SonucHashUyumsuz, "İmzalı XML'in hash'i, imza motorunun beyanıyla eşleşmiyor.", cancellationToken);
        }

        // md.9/md.17 adım 9: bağımsız imza doğrulaması.
        var dogrulamaSonucu = await _dogrulayici.DogrulaAsync(imzaSonucu.SignedUblUtf8, cancellationToken);
        if (!dogrulamaSonucu.GecerliMi)
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                dogrulamaSonucu.HataKodu ?? EBelgeXmlImzaHataKodlari.ImzaDogrulamaHatasi,
                dogrulamaSonucu.HataMesaji ?? "Bağımsız imza doğrulaması başarısız oldu.",
                cancellationToken);
        }

        // md.12/md.17 adım 10: tam XSD doğrulaması - unsigned fazın "tek bilinen bulgu" toleransı YOKTUR.
        try
        {
            _xsdValidator.Validate(imzaSonucu.SignedUblUtf8);
        }
        catch (EBelgeUblXsdValidationFailedException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, ex.HataKodu, $"İmzalı XML, XSD doğrulamasından {ex.Hatalar.Count} hata ile geçemedi.", cancellationToken);
        }

        // md.12/md.17 adım 11: gerçek Java Saxon sidecar Schematron doğrulaması.
        EBelgeSchematronValidationResult schematronSonucu;
        try
        {
            schematronSonucu = await _schematronValidator.ValidateAsync(imzaSonucu.SignedUblUtf8, EBelgeSchematronSidecarOptions.SupportedRuleSetId, cancellationToken);
        }
        catch (EBelgeUblSchematronServiceUnavailableException ex)
        {
            return EBelgeUblImzalamaSonucu.GeciciHata(ex.HataKodu, GuvenliMesaj(ex));
        }
        catch (EBelgeUblSchematronProtocolErrorException ex)
        {
            return EBelgeUblImzalamaSonucu.GeciciHata(ex.HataKodu, GuvenliMesaj(ex));
        }

        if (!schematronSonucu.Valid)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, "EBELGE_SIGNING_SCHEMATRON_VIOLATION", $"İmzalı XML, Schematron doğrulamasından {schematronSonucu.Violations.Count} ihlal ile geçemedi.", cancellationToken);
        }

        // ---- Faz 3: atomik transaction - unique violation'da BİR KEZ yeniden dener ----
        var sonuc = await DenemeBasariAtomikAsync(talep, unsignedArtifact, imzaSonucu, signedBytes, cancellationToken);
        if (sonuc is not null)
        {
            return sonuc;
        }

        sonuc = await DenemeBasariAtomikAsync(talep, unsignedArtifact, imzaSonucu, signedBytes, cancellationToken);
        return sonuc ?? EBelgeUblImzalamaSonucu.GeciciHata(
            "EBELGE_SIGNING_YARIS_DURUMU", "SignedReady artefakt eşzamanlı yazma çakışması - yeniden denenmeli.");
    }

    private async Task<EBelgeUblImzalamaSonucu?> DenemeBasariAtomikAsync(
        EBelgeUblImzalamaTalebi talep,
        EBelgeArtifact unsignedArtifact,
        EBelgeXmlImzaSonucu imzaSonucu,
        byte[] signedBytes,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sahip = await _leaseTransitionService.IsOwnedForJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, cancellationToken);
        if (!sahip)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
        }

        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .FirstAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        var mevcutSigned = await _dbContext.Set<EBelgeArtifact>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.KurumId == talep.KurumId &&
                a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady,
                cancellationToken);

        if (mevcutSigned is not null)
        {
            // md.20: idempotency - AYNI kaynak (Id+hash) İSE, imzaların BYTE-BİREBİR eşleşmesi
            // BEKLENMEZ (xades:SigningTime her denemede FARKLIDIR - bkz. md.21 determinizm notu);
            // mevcut artefakt bağımsız olarak YENİDEN doğrulanır.
            var kaynakEslesiyor = !mevcutSigned.IsDeleted
                && mevcutSigned.KaynakArtifactId == unsignedArtifact.Id
                && string.Equals(mevcutSigned.KaynakArtifactSha256, unsignedArtifact.ArtifactSha256, StringComparison.Ordinal);

            if (!kaynakEslesiyor)
            {
                return await TamamlaKaliciHataAyniTransactiondaAsync(
                    tx, talep, kayit,
                    EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                    "Aynı benzersiz anahtar altında farklı kaynağa bağlı veya soft-delete edilmiş bir SignedReady artefakt zaten mevcut.",
                    cancellationToken);
            }

            var mevcutDogrulama = await _dogrulayici.DogrulaAsync(ImmutableArray.Create(mevcutSigned.Icerik), cancellationToken);
            if (!mevcutDogrulama.GecerliMi)
            {
                return await TamamlaKaliciHataAyniTransactiondaAsync(
                    tx, talep, kayit,
                    EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                    "Mevcut SignedReady artefaktın imzası bağımsız doğrulamadan geçemedi.",
                    cancellationToken);
            }

            kayit.Durum = EBelgeKaydiDurumu.SignedReady;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var tamamIdempotent = await _leaseTransitionService.TryCompleteJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, cancellationToken);
            if (!tamamIdempotent)
            {
                await tx.RollbackAsync(cancellationToken);
                return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
            }

            await tx.CommitAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.AtomikBasarili();
        }

        var yeniSigned = new EBelgeArtifact
        {
            KurumId = talep.KurumId,
            EBelgeKaydiId = talep.EBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.SignedReady,
            RuleSetId = unsignedArtifact.RuleSetId,
            SnapshotSchemaVersion = unsignedArtifact.SnapshotSchemaVersion,
            KaynakSnapshotSha256 = unsignedArtifact.KaynakSnapshotSha256,
            ArtifactSha256 = imzaSonucu.SignedUblSha256,
            Icerik = signedBytes,
            MimeType = "application/xml",
            DosyaAdi = TuretSignedDosyaAdi(unsignedArtifact.DosyaAdi),
            OlusturulmaZamaniUtc = _timeProvider.GetUtcNow().UtcDateTime,
            KaynakArtifactId = unsignedArtifact.Id,
            KaynakArtifactSha256 = unsignedArtifact.ArtifactSha256,
            ImzaProfili = imzaSonucu.ImzaProfili,
            ImzaAlgoritmasi = imzaSonucu.ImzaAlgoritmasi,
            DigestAlgoritmasi = imzaSonucu.DigestAlgoritmasi,
            ImzalayanSertifikaSha256ParmakIzi = imzaSonucu.SertifikaSha256ParmakIzi,
            ImzalamaZamaniUtc = imzaSonucu.ImzalamaZamaniUtc,
        };

        _dbContext.Add(yeniSigned);
        kayit.Durum = EBelgeKaydiDurumu.SignedReady;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsBenzersizlikIhlali(ex))
        {
            await tx.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            _logger.LogInformation(ex, "SignedReady artefakt benzersizlik çakışması - yeniden denenecek (EBelgeKaydiId={EBelgeKaydiId}).", talep.EBelgeKaydiId);
            return null;
        }

        var completeEdildi = await _leaseTransitionService.TryCompleteJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, cancellationToken);
        if (!completeEdildi)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
        }

        await tx.CommitAsync(cancellationToken);
        return EBelgeUblImzalamaSonucu.AtomikBasarili();
    }

    private async Task<EBelgeUblImzalamaSonucu> SonuclandirKaliciHataAtomikAsync(
        EBelgeUblImzalamaTalebi talep,
        bool kayitVarMi,
        string hataKodu,
        string hataMesaji,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sahip = await _leaseTransitionService.IsOwnedForJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, cancellationToken);
        if (!sahip)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
        }

        EBelgeKaydi? kayit = null;
        if (kayitVarMi)
        {
            kayit = await _dbContext.Set<EBelgeKaydi>()
                .FirstOrDefaultAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);
        }

        return await TamamlaKaliciHataAyniTransactiondaAsync(tx, talep, kayit, hataKodu, hataMesaji, cancellationToken);
    }

    /// <summary>
    /// Zaten AÇIK olan (ownership'i doğrulanmış) `tx` transaction'ı İÇİNDE kalıcı hatayı
    /// terminalize eder - yeni/ikinci bir transaction ASLA açılmaz (bkz. Faz 2B.6.2 görev md.3-4
    /// ile AYNI, kanıtlanmış desen).
    /// </summary>
    private async Task<EBelgeUblImzalamaSonucu> TamamlaKaliciHataAyniTransactiondaAsync(
        IDbContextTransaction tx,
        EBelgeUblImzalamaTalebi talep,
        EBelgeKaydi? kayit,
        string hataKodu,
        string hataMesaji,
        CancellationToken cancellationToken)
    {
        if (kayit is not null)
        {
            kayit.Durum = EBelgeKaydiDurumu.ImzalamaKaliciHata;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var failEdildi = await _leaseTransitionService.TryFailJobAsync(
            talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, hataKodu, hataMesaji, retryDelay: null, cancellationToken);

        if (!failEdildi)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
        }

        await tx.CommitAsync(cancellationToken);
        return EBelgeUblImzalamaSonucu.AtomikKaliciHata(hataKodu, hataMesaji);
    }

    private static EBelgeUblImzalamaTalebi ValidateTalepAndNormalize(EBelgeUblImzalamaTalebi talep)
    {
        if (talep.OutboxMesajiId <= 0)
        {
            throw new BaseException("OutboxMesajiId pozitif olmalıdır.", 400);
        }

        if (talep.KurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        if (talep.EBelgeKaydiId <= 0)
        {
            throw new BaseException("EBelgeKaydiId pozitif olmalıdır.", 400);
        }

        var normalizedToken = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateKilitToken(talep.KilitToken);
        return talep with { KilitToken = normalizedToken };
    }

    private static bool IsBenzersizlikIhlali(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx &&
        sqlEx.Errors.Cast<SqlError>().Any(e => e.Number is SqlUniqueConstraintViolation or SqlUniqueIndexViolation);

    private static string TuretSignedDosyaAdi(string unsignedDosyaAdi)
    {
        var tabansiz = Path.GetFileNameWithoutExtension(unsignedDosyaAdi);
        return string.IsNullOrEmpty(tabansiz) ? "ebelge-imzali.xml" : tabansiz + "-imzali.xml";
    }

    private static string GuvenliMesaj(Exception ex) => ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
}

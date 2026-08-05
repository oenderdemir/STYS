using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml;
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
/// Faz 2B.7.1 görev md.5-6 ile GÜNCELLENMİŞTİR: mevcut bir SignedReady artefakt bulunduğunda
/// (idempotent replay), BAĞIMSIZ imza + sıfır-tolerans XSD + GERÇEK Schematron doğrulaması ARTIK
/// SQL transaction'ın TAMAMEN DIŞINDA çalışır - satır kilidi/UPDLOCK bu süre boyunca HİÇ
/// TUTULMAZ. Kısa "Faz 3" transaction'ı yalnız ownership'i yeniden doğrular, artefaktın
/// tx-dışı-doğrulama SIRASINDA DEĞİŞMEDİĞİNİ (immutable Id+hash) teyit eder ve tamamlar. YENİ bir
/// artefakt imzalanırken de AYNI üç kapı (imza/XSD/Schematron) AYNI şekilde tx dışında çalışır -
/// idempotent ve ilk-kez yollar SİMETRİKTİR (md.6).
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

        // Faz 2B.7.1 görev md.5: mevcut SignedReady artefaktı - VARSA - TRANSACTION DIŞINDA okunur.
        // Bulunursa idempotency akışı TAMAMEN tx dışı doğrulamayla yürütülür (bkz.
        // IslemMevcutSignedAsync) - SQL transaction/UPDLOCK yalnız EN SONDA, kısa bir "teyit et ve
        // tamamla" adımı için açılır.
        var mevcutSigned = await OkuMevcutSignedAsync(talep, cancellationToken);
        if (mevcutSigned is not null)
        {
            return await IslemMevcutSignedAsync(talep, unsignedArtifact, mevcutSigned, cancellationToken);
        }

        // ---- Faz 2 (yeni imza): DB dışı imzalama + doğrulama (satır kilidi TUTULMAZ, md.17 adım 8) ----
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
        catch (EBelgeXadesProfiliOnaylanmadiException ex)
        {
            // Konfigürasyon hatası - fail-closed, retry ANLAMSIZDIR (bkz. Faz 2B.7.1 görev md.2).
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
        var dogrulamaSonucu = await DogrulaGuvenliAsync(_dogrulayici, imzaSonucu.SignedUblUtf8, cancellationToken);
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

        // ---- Faz 3: kısa transaction - yeni SignedReady insert eder ----
        var sonuc = await DenemeYeniSignedInsertAtomikAsync(talep, unsignedArtifact, imzaSonucu, signedBytes, cancellationToken);
        if (sonuc is not null)
        {
            return sonuc;
        }

        // Insert unique-violation ile BAŞARISIZ oldu - BAŞKA bir worker (eşzamanlı) kazandı.
        // YENİDEN İMZALAMADAN (gereksiz iş), rakibin YAZDIĞI satırı okuyup AYNI idempotent yoldan
        // (tx dışı tam doğrulama + kısa teyit transaction'ı) tamamlar.
        var raceSigned = await OkuMevcutSignedAsync(talep, cancellationToken);
        if (raceSigned is null)
        {
            return EBelgeUblImzalamaSonucu.GeciciHata(
                EBelgeXmlImzaHataKodlari.YarisDurumu, "SignedReady artefakt eşzamanlı yazma çakışması - yeniden denenmeli.");
        }

        return await IslemMevcutSignedAsync(talep, unsignedArtifact, raceSigned, cancellationToken);
    }

    private Task<EBelgeArtifact?> OkuMevcutSignedAsync(EBelgeUblImzalamaTalebi talep, CancellationToken cancellationToken) =>
        _dbContext.Set<EBelgeArtifact>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.KurumId == talep.KurumId &&
                a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady,
                cancellationToken);

    /// <summary>
    /// <see cref="OkuMevcutSignedAsync"/> İLE AYNI satırı, ama `WITH (UPDLOCK, ROWLOCK)` satır
    /// kilidiyle okur - YALNIZ zaten AÇIK bir transaction İÇİNDE çağrılmalıdır (bkz. Faz 2B.7.2
    /// görev md.4). Kontrol İLE commit ARASINDA satırın DEĞİŞMESİNİ engeller - kilit yalnız BU
    /// KISA okuma+karşılaştırma+commit penceresinde tutulur.
    /// </summary>
    private Task<EBelgeArtifact?> OkuMevcutSignedKilitliAsync(EBelgeUblImzalamaTalebi talep, CancellationToken cancellationToken) =>
        _dbContext.Set<EBelgeArtifact>()
            .FromSqlInterpolated($"""
                SELECT * FROM [muhasebe].[EBelgeArtifactlari] WITH (UPDLOCK, ROWLOCK)
                WHERE [KurumId] = {talep.KurumId} AND [EBelgeKaydiId] = {talep.EBelgeKaydiId}
                  AND [ArtifactTipi] = {(int)EBelgeArtifactTipi.UblXml} AND [ArtifactAsamasi] = {(int)EBelgeArtifactAsamasi.SignedReady}
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Kaynak Unsigned artefaktı `WITH (UPDLOCK, ROWLOCK)` satır kilidiyle okur - YALNIZ zaten
    /// AÇIK bir transaction İÇİNDE çağrılmalıdır (bkz. Faz 2B.7.2 görev md.5).
    /// </summary>
    private Task<EBelgeArtifact?> OkuUnsignedKilitliAsync(EBelgeUblImzalamaTalebi talep, CancellationToken cancellationToken) =>
        _dbContext.Set<EBelgeArtifact>()
            .FromSqlInterpolated($"""
                SELECT * FROM [muhasebe].[EBelgeArtifactlari] WITH (UPDLOCK, ROWLOCK)
                WHERE [KurumId] = {talep.KurumId} AND [EBelgeKaydiId] = {talep.EBelgeKaydiId}
                  AND [ArtifactTipi] = {(int)EBelgeArtifactTipi.UblXml} AND [ArtifactAsamasi] = {(int)EBelgeArtifactAsamasi.Unsigned}
                """)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// `dogrulayici.DogrulaAsync`'i, BEKLENMEDİK bir parse/kriptografi exception'ının (savunma
    /// derinliği - doğrulayıcı KENDİSİ zaten bunları yakalar, bkz.
    /// <see cref="EBelgeXmlImzaDogrulayici.DogrulaAsync"/>) generic outbox katmanına KAÇIP geçici
    /// bir retry'a DÖNÜŞMESİNİ engeller (bkz. Faz 2B.7.2 görev md.2, md.9 - "genel catch (Exception)
    /// ile doğrulama hatalarını gizleme" - bu yüzden yalnız AÇIKÇA SINIFLANDIRILMIŞ, beklenen
    /// parse/kriptografi exception tipleri yakalanır). Böyle bir exception YAKALANIRSA, sonuç HER
    /// ZAMAN KALICI bir Gecersiz olarak sınıflandırılır - hiçbir kişisel veri/XML/sertifika
    /// içeriği İÇERMEYEN SABİT bir mesajla.
    /// </summary>
    private static async Task<EBelgeXmlImzaDogrulamaSonucu> DogrulaGuvenliAsync(
        IEBelgeXmlImzaDogrulayici dogrulayici,
        ImmutableArray<byte> xmlBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dogrulayici.DogrulaAsync(xmlBytes, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is XmlException or FormatException or CryptographicException or ArgumentException or OverflowException or InvalidOperationException)
        {
            return EBelgeXmlImzaDogrulamaSonucu.Gecersiz(
                EBelgeXmlImzaHataKodlari.BozukImzaBelgesi,
                "İmzalı belge, ayrıştırma veya kriptografik doğrulama sırasında geçersiz/bozuk bulundu.");
        }
    }

    /// <summary>
    /// Mevcut bir SignedReady artefaktı (idempotent replay YA DA yeni-insert yarışını KAYBEDEN
    /// bir denemenin rakip satırı) işler - bkz. Faz 2B.7.1 görev md.5-6. Kaynak eşleşme
    /// kontrolünden SONRA, imza/XSD/Schematron doğrulaması TAMAMEN SQL transaction'ın DIŞINDA
    /// çalışır (satır kilidi TUTULMAZ) - YENİ bir artefaktın imzalanma akışıyla (md.17 adım 8-11)
    /// TAM SİMETRİKTİR (md.6). Yalnız EN SONDAKİ kısa transaction, ownership'i ve artefaktın
    /// (immutable Id+hash) tx-dışı doğrulama SIRASINDA DEĞİŞMEDİĞİNİ teyit eder.
    /// </summary>
    private async Task<EBelgeUblImzalamaSonucu> IslemMevcutSignedAsync(
        EBelgeUblImzalamaTalebi talep,
        EBelgeArtifact unsignedArtifact,
        EBelgeArtifact mevcutSigned,
        CancellationToken cancellationToken)
    {
        // Faz 2B.7.2 görev md.3: EXACT-BYTE hash kontrolü - kayıtlı ArtifactSha256 sütunu,
        // İÇERİĞİN (Icerik) GERÇEK SHA-256'sıyla eşleşmelidir. Bu, İMZA DOĞRULAMASINDAN ÖNCE
        // yapılır - hash uyuşmazlığı varsa imza doğrulamasına HİÇ DEVAM EDİLMEZ (stored hash
        // sütununa, gerçekten hesaplamadan GÜVENİLMEZ). Bu hash - "tx-dışı doğrulamada kullanılan
        // hash" - Faz 3'teki kısa transaction'da (bkz. md.4) YENİDEN kullanılacaktır.
        var gercekSignedHash = Convert.ToHexString(SHA256.HashData(mevcutSigned.Icerik));
        if (!string.Equals(gercekSignedHash, mevcutSigned.ArtifactSha256, StringComparison.Ordinal))
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.MevcutArtifactHashUyumsuz,
                "Mevcut SignedReady artefaktın kayıtlı hash'i, saklanan içerikle eşleşmiyor.",
                cancellationToken);
        }

        // md.20: idempotency - AYNI kaynak (Id+hash) İSE, imzaların BYTE-BİREBİR eşleşmesi
        // BEKLENMEZ (xades:SigningTime her denemede FARKLIDIR - bkz. md.21 determinizm notu).
        var kaynakEslesiyor = !mevcutSigned.IsDeleted
            && mevcutSigned.KaynakArtifactId == unsignedArtifact.Id
            && string.Equals(mevcutSigned.KaynakArtifactSha256, unsignedArtifact.ArtifactSha256, StringComparison.Ordinal);

        if (!kaynakEslesiyor)
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                "Aynı benzersiz anahtar altında farklı kaynağa bağlı veya soft-delete edilmiş bir SignedReady artefakt zaten mevcut.",
                cancellationToken);
        }

        // ---- Faz 2 (idempotent): DB dışı tam doğrulama (satır kilidi TUTULMAZ) ----
        var mevcutBytes = ImmutableArray.Create(mevcutSigned.Icerik);

        // Faz 2B.7.2 görev md.2: `_dogrulayici.DogrulaAsync` BEKLENMEDİK bir parse/kriptografi
        // exception'ı üretirse (savunma derinliği - doğrulayıcı KENDİSİ zaten bunları yakalar),
        // bu generic outbox katmanına KAÇIP geçici bir retry'a DÖNÜŞMEZ - `DogrulaGuvenliAsync`
        // ile HER ZAMAN KALICI olarak sınıflandırılır.
        var mevcutDogrulama = await DogrulaGuvenliAsync(_dogrulayici, mevcutBytes, cancellationToken);
        if (!mevcutDogrulama.GecerliMi)
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                $"Mevcut SignedReady artefaktın imzası bağımsız doğrulamadan geçemedi: {mevcutDogrulama.HataKodu} {mevcutDogrulama.HataMesaji}",
                cancellationToken);
        }

        try
        {
            _xsdValidator.Validate(mevcutBytes);
        }
        catch (EBelgeUblXsdValidationFailedException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                $"Mevcut SignedReady artefakt, XSD doğrulamasından {ex.Hatalar.Count} hata ile geçemedi.",
                cancellationToken);
        }

        EBelgeSchematronValidationResult mevcutSchematron;
        try
        {
            mevcutSchematron = await _schematronValidator.ValidateAsync(mevcutBytes, EBelgeSchematronSidecarOptions.SupportedRuleSetId, cancellationToken);
        }
        catch (EBelgeUblSchematronServiceUnavailableException ex)
        {
            return EBelgeUblImzalamaSonucu.GeciciHata(ex.HataKodu, GuvenliMesaj(ex));
        }
        catch (EBelgeUblSchematronProtocolErrorException ex)
        {
            return EBelgeUblImzalamaSonucu.GeciciHata(ex.HataKodu, GuvenliMesaj(ex));
        }

        if (!mevcutSchematron.Valid)
        {
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.SignedArtifactIdempotencyConflict,
                $"Mevcut SignedReady artefakt, Schematron doğrulamasından {mevcutSchematron.Violations.Count} ihlal ile geçemedi.",
                cancellationToken);
        }

        // ---- Faz 3: KISA transaction - ownership + "artefakt tx-dışı doğrulama sırasında
        // DEĞİŞMEDİ" teyidi + tamamlama ----
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sahip = await _leaseTransitionService.IsOwnedForJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.UblImzala, talep.KilitToken, cancellationToken);
        if (!sahip)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.SahiplikKaybedildi();
        }

        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .FirstAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        // Faz 2B.7.2 görev md.4: yalnız hash SÜTUNUNU karşılaştırmak YETERLİ DEĞİLDİR - artefakt
        // satırı UYGUN bir satır kilidiyle (UPDLOCK/ROWLOCK) YENİDEN okunur ve ID/KurumId/
        // EBelgeKaydiId/ArtifactAsamasi/IsDeleted/KaynakArtifactId/KaynakArtifactSha256/
        // ArtifactSha256 alanlarının TAMAMI VE İçeriğin (Icerik) EXACT SHA-256'sı (yalnız stored
        // sütun DEĞİL) tx-dışı doğrulamada kullanılanla (`gercekSignedHash`) yeniden karşılaştırılır.
        // Kontrol İLE commit ARASINDA satırın DEĞİŞMESİNİ engelleyen bu kilit, YALNIZ bu KISA
        // pencerede tutulur - imza/XSD/sidecar çağrısı bu kilit ALTINDA HİÇ ÇALIŞMAZ (zaten Faz
        // 2'de, tx açılmadan ÖNCE tamamlanmıştır).
        var yenidenOkunanSigned = await OkuMevcutSignedKilitliAsync(talep, cancellationToken);
        var yenidenHesaplananSignedHash = yenidenOkunanSigned is null ? null : Convert.ToHexString(SHA256.HashData(yenidenOkunanSigned.Icerik));

        var satirDegismedi = yenidenOkunanSigned is not null
            && yenidenOkunanSigned.Id == mevcutSigned.Id
            && yenidenOkunanSigned.KurumId == talep.KurumId
            && yenidenOkunanSigned.EBelgeKaydiId == talep.EBelgeKaydiId
            && yenidenOkunanSigned.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady
            && !yenidenOkunanSigned.IsDeleted
            && yenidenOkunanSigned.KaynakArtifactId == mevcutSigned.KaynakArtifactId
            && string.Equals(yenidenOkunanSigned.KaynakArtifactSha256, mevcutSigned.KaynakArtifactSha256, StringComparison.Ordinal)
            && string.Equals(yenidenOkunanSigned.ArtifactSha256, mevcutSigned.ArtifactSha256, StringComparison.Ordinal)
            && string.Equals(yenidenHesaplananSignedHash, gercekSignedHash, StringComparison.Ordinal);

        if (!satirDegismedi)
        {
            // Bkz. görev md.5 "transaction dışı doğrulama sonrasında artifact hash'i değişmişse
            // sonuç kullanılmaz" - önceki doğrulama SONUCU ARTIK GÜVENİLMEZ, geçici hata olarak
            // raporlanır (üst katman/outbox retry policy yeniden dener).
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.GeciciHata(
                EBelgeXmlImzaHataKodlari.YarisDurumu, "SignedReady artefakt, bağımsız tx-dışı doğrulama sırasında değişti - yeniden denenmeli.");
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

    /// <summary>
    /// YENİ bir SignedReady artefaktı, TEK kısa transaction içinde insert eder (bkz. görev md.17).
    /// Bu noktaya gelindiğinde imza/bağımsız doğrulama/XSD/Schematron ZATEN (Faz 2'de, tx DIŞINDA)
    /// tamamlanmıştır - bu metot yalnız ownership doğrulaması + kalıcılaştırma + tamamlamadan
    /// ibarettir. Unique-violation'da (BAŞKA bir worker kazandı) `null` döner - çağıran, YENİDEN
    /// İMZALAMADAN rakip satırı okuyup idempotent yoldan (<see cref="IslemMevcutSignedAsync"/>)
    /// devam eder.
    /// </summary>
    private async Task<EBelgeUblImzalamaSonucu?> DenemeYeniSignedInsertAtomikAsync(
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

        // Faz 2B.7.2 görev md.5: SignedReady insert EDİLMEDEN ÖNCE, kaynak Unsigned artefaktı
        // UYGUN bir satır kilidiyle (UPDLOCK/ROWLOCK) YENİDEN okunur ve kimlik+hash bütünlüğü
        // BAĞIMSIZ olarak yeniden doğrulanır - imzalama (Faz 2, tx dışı) İLE bu nokta ARASINDA
        // kaynak DEĞİŞMİŞ (soft-delete edilmiş) OLABİLİR.
        var yenidenOkunanUnsigned = await OkuUnsignedKilitliAsync(talep, cancellationToken);

        if (yenidenOkunanUnsigned is null || yenidenOkunanUnsigned.IsDeleted)
        {
            // Kaynak imzalama SIRASINDA kayboldu/soft-delete edildi - GEÇİCİ bir yarış durumudur,
            // YENİ bir claim ile yeniden imzalanmalıdır (bkz. görev md.5).
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.GeciciHata(
                EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti,
                "Unsigned kaynak artefakt, imzalama sırasında değişti/soft-delete edildi.");
        }

        var yenidenHesaplananUnsignedHash = Convert.ToHexString(SHA256.HashData(yenidenOkunanUnsigned.Icerik));
        if (!string.Equals(yenidenHesaplananUnsignedHash, yenidenOkunanUnsigned.ArtifactSha256, StringComparison.Ordinal))
        {
            // Kaydın KENDİ ArtifactSha256 sütunu, GERÇEK içeriğiyle eşleşmiyor - bu KALICI bir
            // bütünlük sorunudur (bkz. görev md.5, "kalıcı olarak hash'i bozuksa"). `tx` ÖNCE
            // rollback edilir - `SonuclandirKaliciHataAtomikAsync` KENDİ transaction'ını açar,
            // aynı anda İKİ açık transaction OLAMAZ (bkz. Faz 2B.6.2 kanıtlanmış deseni).
            await tx.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return await SonuclandirKaliciHataAtomikAsync(
                talep, kayitVarMi: true,
                EBelgeXmlImzaHataKodlari.KaynakHashUyumsuz,
                "Unsigned kaynak artefaktın kayıtlı hash'i, saklanan içerikle eşleşmiyor (imzalama sonucu kalıcılaştırılırken tespit edildi).",
                cancellationToken);
        }

        if (!string.Equals(yenidenOkunanUnsigned.ArtifactSha256, unsignedArtifact.ArtifactSha256, StringComparison.Ordinal) ||
            !string.Equals(unsignedArtifact.ArtifactSha256, imzaSonucu.KaynakUnsignedUblSha256, StringComparison.Ordinal))
        {
            // Kaynağın hash'i KENDİ İÇİNDE tutarlı ama tx-dışı imzalama SIRASINDA KULLANILANDAN
            // FARKLI bulundu - GEÇİCİ bir yarış durumudur (bkz. görev md.5).
            await tx.RollbackAsync(cancellationToken);
            return EBelgeUblImzalamaSonucu.GeciciHata(
                EBelgeXmlImzaHataKodlari.KaynakImzalamaSirasindaDegisti,
                "Unsigned kaynak artefakt, imzalama sırasında kullanılan hash'ten farklı bulundu.");
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
            _logger.LogInformation(ex, "SignedReady artefakt benzersizlik çakışması - rakip satır idempotent yoldan işlenecek (EBelgeKaydiId={EBelgeKaydiId}).", talep.EBelgeKaydiId);
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

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// <see cref="IEBelgeArtefaktOlusturmaService"/>'in GERÇEK implementasyonu - immutable
/// EBelgeKaydi/EBelgeSnapshot kaydından V2 canonical snapshot'ı okur, GERÇEK
/// <see cref="IEBelgeUblRenderer"/>'ı (yerel XSD + gerçek Java Saxon sidecar Schematron
/// doğrulaması dahil) çağırır ve başarılı sonucu immutable <see cref="EBelgeArtifact"/> olarak
/// kalıcılaştırır (bkz. Faz 2B.6.1/2B.6.2 görev - lease ownership, çapraz kayıt bağlama ve
/// atomik sonuç kaydı düzeltmeleri).
///
/// Kesin akış: claim → DB DIŞI render (satır kilidi TUTULMAZ) → lease YENİDEN doğrulama
/// (`OutboxId + KurumId + EBelgeKaydiId + IsTuru + Token + Expiry` - bkz.
/// <see cref="IEBelgeOutboxLeaseTransitionService.IsOwnedForJobAsync"/>) → artefakt +
/// EBelgeKaydi + outbox TEK atomik transaction. Başarı VE kalıcı hata yolları, KENDİ
/// transaction'larında outbox geçişini de (iş-türü-farkında TryCompleteJobAsync/
/// TryFailJobAsync üzerinden, AYNI ambient transaction içinde) TAMAMLAR -
/// <see cref="EBelgeOutboxMesajIslemeService"/> bu durumda İKİNCİ bir geçiş çağrısı YAPMAZ
/// (bkz. EBelgeArtefaktOlusturmaSonucuTuru.Atomik*). Yalnız GEÇİCİ hatalar (EBelgeKaydi'yi hiç
/// değiştirmediğinden) ESKİ, ayrı/retry-policy'li (genel, artifact-farkında OLMAYAN)
/// TryFailAsync akışını kullanmaya devam eder.
///
/// Kalıcı hata yolları (idempotency conflict dahil) HER ZAMAN TEK bir açık transaction içinde
/// terminalize edilir - rollback edilmiş ama dispose edilmemiş bir transaction üzerinde İKİNCİ
/// bir `BeginTransactionAsync` ASLA çağrılmaz (bkz. Faz 2B.6.2 görev md.3-4,
/// <see cref="TamamlaKaliciHataAyniTransactiondaAsync"/>).
/// </summary>
public sealed class EBelgeArtefaktOlusturmaService : IEBelgeArtefaktOlusturmaService
{
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    private static readonly Regex GuvenliDosyaAdiKarakterleri = new("[^A-Za-z0-9_-]", RegexOptions.Compiled);

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeCanonicalSnapshotV2Reader _snapshotReader;
    private readonly IEBelgeUblRenderer _renderer;
    private readonly IEBelgeOutboxLeaseTransitionService _leaseTransitionService;
    private readonly IEBelgeSigningActivationGate _signingActivationGate;
    private readonly IEBelgeKurumPolitikaServisi _kurumPolitikaServisi;
    private readonly IEBelgeKurumPolitikaTransactionGuard _kurumPolitikaGuard;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EBelgeArtefaktOlusturmaService> _logger;

    public EBelgeArtefaktOlusturmaService(
        StysAppDbContext dbContext,
        IEBelgeCanonicalSnapshotV2Reader snapshotReader,
        IEBelgeUblRenderer renderer,
        IEBelgeOutboxLeaseTransitionService leaseTransitionService,
        IEBelgeSigningActivationGate signingActivationGate,
        IEBelgeKurumPolitikaServisi kurumPolitikaServisi,
        IEBelgeKurumPolitikaTransactionGuard kurumPolitikaGuard,
        TimeProvider timeProvider,
        ILogger<EBelgeArtefaktOlusturmaService> logger)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _renderer = renderer;
        _leaseTransitionService = leaseTransitionService;
        _signingActivationGate = signingActivationGate;
        _kurumPolitikaServisi = kurumPolitikaServisi;
        _kurumPolitikaGuard = kurumPolitikaGuard;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<EBelgeArtefaktOlusturmaSonucu?> OlusturAsync(
        EBelgeArtefaktOlusturmaTalebi talep,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(talep);
        talep = ValidateTalepAndNormalize(talep);

        // ---- Faz 1: kısa okuma (açık transaction yok) ----
        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .Include(x => x.Snapshot)
            .FirstOrDefaultAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        if (kayit is null)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: false, "EBELGE_KAYDI_BULUNAMADI", "E-belge kaydı bulunamadı.", cancellationToken);
        }

        if (kayit.Snapshot is null)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, "EBELGE_SNAPSHOT_BULUNAMADI", "E-belge kaydına ait canonical snapshot bulunamadı.", cancellationToken);
        }

        var snapshotSchemaVersionYaziIle = kayit.Snapshot.SnapshotSchemaVersion;
        var snapshotBytes = Encoding.UTF8.GetBytes(kayit.Snapshot.CanonicalJson);
        var snapshotHash = kayit.Snapshot.CanonicalSha256;

        EBelgeCanonicalSnapshotV2 snapshot;
        try
        {
            // IEBelgeCanonicalSnapshotV2Reader.Read hash uyuşmazlığını ve şema sürümünü (yalnız
            // "2") KENDİSİ doğrular - snapshot burada CANLI entity'lerden YENİDEN ÜRETİLMEZ,
            // yalnız zaten saklanmış immutable JSON okunur.
            snapshot = _snapshotReader.Read(snapshotBytes, snapshotHash);
        }
        catch (EBelgeCanonicalSnapshotException)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, kayitVarMi: true, EBelgeCanonicalSnapshotException.SafeErrorCode, EBelgeCanonicalSnapshotException.SafeMessage, cancellationToken);
        }

        // ---- Faz 2: DB dışı render (sidecar HTTP çağrısı dahil) - satır kilidi TUTULMAZ ----
        EBelgeUblRenderSonucu renderSonuc;
        try
        {
            renderSonuc = await _renderer.RenderAsync(snapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EBelgeUblRenderSnapshotVersionUnsupportedException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, true, EBelgeUblRenderSnapshotVersionUnsupportedException.SafeErrorCode, GuvenliMesaj(ex), cancellationToken);
        }
        catch (EBelgeUblRenderScopeUnsupportedException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, true, EBelgeUblRenderScopeUnsupportedException.SafeErrorCode, GuvenliMesaj(ex), cancellationToken);
        }
        catch (EBelgeUblAuthoritativeFieldMissingException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, true, EBelgeUblAuthoritativeFieldMissingException.SafeErrorCode, GuvenliMesaj(ex), cancellationToken);
        }
        catch (EBelgeUblKuralSetiManifestException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, true, EBelgeUblKuralSetiManifestException.SafeErrorCode, GuvenliMesaj(ex), cancellationToken);
        }
        catch (EBelgeUblMonetaryTotalMismatchException)
        {
            // Snapshot immutable olduğundan AYNI mesajı yeniden denemek sorunu ÇÖZMEZ - kalıcı
            // düzeltilebilir iş hatası: belge verisi düzeltilip YENİ bir fatura/snapshot/outbox
            // mesajı üretilmelidir (bkz. görev md.10, "düzeltilebilir iş hataları").
            return await SonuclandirKaliciHataAtomikAsync(
                talep, true,
                EBelgeUblMonetaryTotalMismatchException.SafeErrorCode,
                "Belge toplamları tutarsız - snapshot immutable olduğundan bu mesaj tekrar denenerek çözülemez; belge düzeltilip yeni bir kesim/snapshot üretilmelidir.",
                cancellationToken);
        }
        catch (EBelgeUblXsdValidationFailedException ex)
        {
            // Hata mesajı METNİ YAZILMAZ - XSD hata metinleri XML'den alınan alan DEĞERLERİNİ
            // (ör. ProfileID) echo edebilir; yalnız sayı ve sabit şablon saklanır (bkz. md.14).
            return await SonuclandirKaliciHataAtomikAsync(
                talep, true, EBelgeUblXsdValidationFailedException.SafeErrorCode,
                $"XSD doğrulaması {ex.Hatalar.Count} hata ile başarısız oldu.", cancellationToken);
        }
        catch (EBelgeUblSchematronValidationFailedException ex)
        {
            // Aynı gerekçe: schematron ihlal metinleri alan DEĞERLERİNİ echo edebilir (bkz.
            // sidecar SVRL çıktısı) - yalnız ihlal SAYISI saklanır, ham metin YOK.
            return await SonuclandirKaliciHataAtomikAsync(
                talep, true, EBelgeUblSchematronValidationFailedException.SafeErrorCode,
                $"Schematron doğrulaması {ex.Ihlaller.Count} ihlal ile başarısız oldu.", cancellationToken);
        }
        catch (EBelgeUblRuleSetArtifactInvalidException ex)
        {
            return await SonuclandirKaliciHataAtomikAsync(talep, true, EBelgeUblRuleSetArtifactInvalidException.SafeErrorCode, GuvenliMesaj(ex), cancellationToken);
        }
        catch (EBelgeUblSchematronServiceUnavailableException ex)
        {
            // Geçici hata - EBelgeKaydi DEĞİŞMEZ, transaction/atomik geçiş GEREKMEZ (bkz. görev md.5).
            return EBelgeArtefaktOlusturmaSonucu.GeciciHata(EBelgeUblSchematronServiceUnavailableException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblSchematronProtocolErrorException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.GeciciHata(EBelgeUblSchematronProtocolErrorException.SafeErrorCode, GuvenliMesaj(ex));
        }

        // ---- Runtime hash doğrulaması (md.7): XML yeniden serialize EDİLMEZ, yalnız EXACT
        // bytes üzerinden bağımsız SHA-256 hesaplanır ve renderer'ın kendi beyanıyla karşılaştırılır. ----
        var xmlBytes = renderSonuc.UnsignedUblUtf8.ToArray();
        var bagimsizHash = Convert.ToHexString(SHA256.HashData(xmlBytes));
        if (!string.Equals(bagimsizHash, renderSonuc.UnsignedUblSha256, StringComparison.Ordinal))
        {
            _logger.LogError("Runtime artifact hash uyuşmazlığı tespit edildi (EBelgeKaydiId={EBelgeKaydiId}).", talep.EBelgeKaydiId);
            return await SonuclandirKaliciHataAtomikAsync(
                talep, true, "EBELGE_ARTIFACT_HASH_MISMATCH",
                "Renderer sonucu hash'i, bağımsız hesaplanan hash ile eşleşmiyor.", cancellationToken);
        }

        // ---- Faz 3: atomik başarı transaction'ı - unique violation'da BİR KEZ yeniden dener ----
        var sonuc = await DenemeBasariAtomikAsync(talep, renderSonuc, xmlBytes, snapshotSchemaVersionYaziIle, snapshotHash, snapshot, cancellationToken);
        if (sonuc is not null)
        {
            return sonuc;
        }

        sonuc = await DenemeBasariAtomikAsync(talep, renderSonuc, xmlBytes, snapshotSchemaVersionYaziIle, snapshotHash, snapshot, cancellationToken);
        return sonuc ?? EBelgeArtefaktOlusturmaSonucu.GeciciHata(
            "EBELGE_ARTIFACT_YARIS_DURUMU", "Artefakt eşzamanlı yazma çakışması - yeniden denenmeli.");
    }

    /// <summary>
    /// Tek bir atomik deneme: lease ownership doğrula → (mevcut artefakt varsa idempotency
    /// karşılaştır, yoksa insert et) → EBelgeKaydi.Durum = UnsignedUblHazir → outbox Tamamlandi
    /// → TEK transaction ile commit. Benzersizlik ihlaliyle karşılaşırsa rollback yapar, DbContext
    /// ChangeTracker'ı temizler ve `null` döner (çağıran BİR KEZ daha dener).
    /// </summary>
    private async Task<EBelgeArtefaktOlusturmaSonucu?> DenemeBasariAtomikAsync(
        EBelgeArtefaktOlusturmaTalebi talep,
        EBelgeUblRenderSonucu renderSonuc,
        byte[] xmlBytes,
        string snapshotSchemaVersionYaziIle,
        string snapshotHash,
        EBelgeCanonicalSnapshotV2 snapshot,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sahip = await _leaseTransitionService.IsOwnedForJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, cancellationToken);
        if (!sahip)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
        }

        // Faz 2B.10.2 görev md.3/md.9 - lease ownership (outbox row) doğrulandıktan SONRA, ama
        // artifact/EBelgeKaydi DEĞİŞTİRİLMEDEN ÖNCE, kurum politika satırı `WITH (UPDLOCK,
        // HOLDLOCK, ROWLOCK)` ile KİLİTLENİR - normal AsNoTracking()/READ COMMITTED okuma "aynı
        // transaction içinde okundu" olsa bile bir SERIALIZATION garantisi VERMEZ (bkz. görev
        // md.1). Kilit alındıktan SONRA, uygunluk bu SABİTLENMİŞ anlık görüntü ÜZERİNDEN
        // değerlendirilir - render/sidecar çağrısı (Faz 2) SÜRESİNCE politika kapanmış OLABİLİR,
        // ama bu KİLİT alındıktan SONRA artık BAŞKA hiçbir writer politikayı DEĞİŞTİREMEZ (lock
        // ordering: outbox ownership row → kurum policy row → EBelgeKaydi/artifact yazmaları,
        // bkz. görev md.9). Uygun değilse: artifact insert EDİLMEZ, EBelgeKaydi.Durum DEĞİŞMEZ,
        // outbox KENDİ transaction'ında güvenli bekleme durumuna alınır (terminalize EDİLMEZ).
        var kilitliPolitika = await _kurumPolitikaGuard.KilitleVeOkuAsync(talep.KurumId, cancellationToken);
        var uygunluk = await _kurumPolitikaServisi.DegerlendirIslemUygunlugunuKilitliAsync(talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, kilitliPolitika, cancellationToken);
        if (!uygunluk.UygunMu)
        {
            var geriAlindi = await _leaseTransitionService.TryReleasePolicyBlockedAsync(
                talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, cancellationToken);

            if (!geriAlindi)
            {
                await tx.RollbackAsync(cancellationToken);
                return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
            }

            await tx.CommitAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.AtomikPolitikaBloklu();
        }

        // Taze, TRANSACTION İÇİNDE izlenen (tracked) bir EBelgeKaydi al - Faz 1'deki referans,
        // önceki bir denemede rollback edilmiş olabilir (stale tracking riskini önler).
        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .FirstAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        // Soft-delete edilmiş kayıt bile benzersizlik rezervasyonunu KORUR (bkz. görev md.6) -
        // bu yüzden idempotency/benzersizlik sorgusu IgnoreQueryFilters() KULLANIR.
        var mevcutArtifact = await _dbContext.Set<EBelgeArtifact>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.KurumId == talep.KurumId &&
                a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned,
                cancellationToken);

        if (mevcutArtifact is not null)
        {
            if (!EslesiyorMu(mevcutArtifact, renderSonuc, snapshotHash))
            {
                // Soft-delete edilmiş VEYA farklı hash'li bir rezervasyon - mali/yasal artefakt
                // silinemez sözleşmesi nedeniyle bu, veri bütünlüğü ihlali kabul edilir (bkz.
                // görev md.6) - sessiz başarı YOK, kalıcı çakışma hatası. AYNI (zaten ownership'i
                // doğrulanmış) `tx` transaction'ı İÇİNDE terminalize edilir - rollback edip
                // İKİNCİ bir transaction AÇILMAZ (bkz. Faz 2B.6.2 görev md.3 - önceki turda bu,
                // rollback edilmiş ama dispose edilmemiş bir transaction üzerinde yeniden
                // `BeginTransactionAsync` çağrısı riski taşıyordu).
                return await TamamlaKaliciHataAyniTransactiondaAsync(
                    tx, talep, kayit,
                    EBelgeArtifactIdempotencyConflictException.SafeErrorCode,
                    "Aynı benzersiz anahtar altında farklı içerikli veya soft-delete edilmiş bir artefakt zaten mevcut.",
                    cancellationToken);
            }

            // İdempotent başarı: artefakt zaten doğru - yalnız EBelgeKaydi/outbox durumlarının
            // da tutarlı olduğundan emin ol (önceki bir deneme artefaktı yazıp tamamlama
            // aşamasında kesilmiş olabilir - bkz. görev md.6 son cümlesi).
            kayit.Durum = EBelgeKaydiDurumu.UnsignedUblHazir;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var tamamIdempotent = await _leaseTransitionService.TryCompleteJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, cancellationToken);
            if (!tamamIdempotent)
            {
                await tx.RollbackAsync(cancellationToken);
                return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
            }

            await tx.CommitAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.AtomikBasarili();
        }

        var yeniArtifact = new EBelgeArtifact
        {
            KurumId = talep.KurumId,
            EBelgeKaydiId = talep.EBelgeKaydiId,
            ArtifactTipi = EBelgeArtifactTipi.UblXml,
            ArtifactAsamasi = EBelgeArtifactAsamasi.Unsigned,
            RuleSetId = renderSonuc.KuralSetiKimligi,
            SnapshotSchemaVersion = int.Parse(snapshotSchemaVersionYaziIle),
            KaynakSnapshotSha256 = snapshotHash,
            ArtifactSha256 = renderSonuc.UnsignedUblSha256,
            Icerik = xmlBytes,
            MimeType = "application/xml",
            DosyaAdi = TurentDosyaAdi(snapshot),
            OlusturulmaZamaniUtc = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _dbContext.Add(yeniArtifact);
        kayit.Durum = EBelgeKaydiDurumu.UnsignedUblHazir;

        // Faz 2B.7 görev md.18 + Faz 2B.10 görev md.12: unsigned artifact İLK KEZ başarıyla
        // oluşturulduğunda, ÜÇ koşulun HEPSİ sağlanırsa AYNI transaction içinde TEK bir UblImzala
        // outbox mesajı oluşturulur: (1) immutable satış e-belge kararı YerelImzaOlustur=true
        // (ör. GibPortal İÇİN bu HER ZAMAN false'dur - "son durum UnsignedUblHazir" beklenir, bu
        // HATA DEĞİLDİR), (2) global signing aktivasyon kapısı AÇIK, (3) GÜNCEL kurum politikası
        // hâlâ aktif (karar anından SONRA pasife alınmış OLABİLİR). Herhangi biri false ise
        // unsigned artifact YİNE DE atomik olarak TAMAMLANIR - yalnız UblImzala mesajı
        // OLUŞTURULMAZ. İdempotent-eşleşme dalında (yukarıda) BUNU YAPMAYIZ - aksi halde
        // (EBelgeKaydiId, IsTuru) benzersizlik indeksini ihlal eder (kapı yalnız İLK başarılı
        // oluşturmada değerlendirilir).
        if (await ImzalamaMesajiOlusturulmaliMiAsync(talep, cancellationToken))
        {
            _dbContext.Add(new EBelgeOutboxMesaji
            {
                KurumId = talep.KurumId,
                EBelgeKaydiId = talep.EBelgeKaydiId,
                IsTuru = EBelgeOutboxIsTuru.UblImzala,
                Durum = EBelgeOutboxDurumu.Bekliyor,
                DenemeSayisi = 0,
            });
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsBenzersizlikIhlali(ex))
        {
            // Yarış durumu: başka bir worker aynı artefaktı bu sırada eklemiş olabilir (bkz.
            // görev md.12, "iki paralel worker tek artifact üretir"). TÜM transaction rollback
            // edilir (artefakt İLE EBelgeKaydi güncellemesi birlikte geri alınır) ve ChangeTracker
            // temizlenir - çağıran taze bir denemeyle idempotency yoluna düşecektir.
            await tx.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            _logger.LogInformation(ex, "Artefakt benzersizlik çakışması - yeniden denenecek (EBelgeKaydiId={EBelgeKaydiId}).", talep.EBelgeKaydiId);
            return null;
        }

        var completeEdildi = await _leaseTransitionService.TryCompleteJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, cancellationToken);
        if (!completeEdildi)
        {
            // Lease, render + insert sırasında dolmuş/kaybedilmiş - TÜM transaction (artefakt +
            // EBelgeKaydi.Durum dahil) geri alınır; hiçbir şey KALICILAŞMAZ (bkz. görev md.2).
            await tx.RollbackAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
        }

        await tx.CommitAsync(cancellationToken);
        return EBelgeArtefaktOlusturmaSonucu.AtomikBasarili();
    }

    /// <summary>
    /// Kalıcı hata sonucunu, lease ownership'i yeniden doğrulayarak, TEK transaction içinde
    /// EBelgeKaydi.Durum=UnsignedUblKaliciHata + outbox terminal Hata (SonrakiDenemeZamaniUtc=null)
    /// olarak ATOMİK biçimde kaydeder (bkz. görev md.4).
    /// </summary>
    private async Task<EBelgeArtefaktOlusturmaSonucu> SonuclandirKaliciHataAtomikAsync(
        EBelgeArtefaktOlusturmaTalebi talep,
        bool kayitVarMi,
        string hataKodu,
        string hataMesaji,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var sahip = await _leaseTransitionService.IsOwnedForJobAsync(talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, cancellationToken);
        if (!sahip)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
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
    /// Talep alanlarını (bkz. Faz 2B.6.2 görev md.5) doğrular ve `KilitToken`'ı kanonik forma
    /// normalize eder. `KilitBitisZamaniUtc` yalnız BİLGİ amaçlıdır - hiçbir doğrulama/ownership
    /// kararı bu alana DAYANMAZ; DB'deki gerçek `KilitBitisZamaniUtc` (SYSUTCDATETIME() ile
    /// karşılaştırılan) OTORİTERDİR (bkz. IsOwnedForJobAsync SQL'i). İstemciden gelen bu
    /// timestamp'e güvenilmez.
    /// </summary>
    private static EBelgeArtefaktOlusturmaTalebi ValidateTalepAndNormalize(EBelgeArtefaktOlusturmaTalebi talep)
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

    /// <summary>
    /// Zaten AÇIK olan (ownership'i doğrulanmış) `tx` transaction'ı İÇİNDE, kalıcı hatayı
    /// terminalize eder: `EBelgeKaydi.Durum = UnsignedUblKaliciHata` (kayit varsa) →
    /// iş-türü-farkında `TryFailJobAsync` → TEK commit. Yeni/ikinci bir transaction ASLA
    /// AÇILMAZ (bkz. Faz 2B.6.2 görev md.3-4 - rollback edilmiş/dispose edilmemiş bir
    /// transaction üzerinde ikinci `BeginTransactionAsync` çağrısı riskini YAPISAL olarak
    /// ortadan kaldırır).
    /// </summary>
    private async Task<EBelgeArtefaktOlusturmaSonucu> TamamlaKaliciHataAyniTransactiondaAsync(
        IDbContextTransaction tx,
        EBelgeArtefaktOlusturmaTalebi talep,
        EBelgeKaydi? kayit,
        string hataKodu,
        string hataMesaji,
        CancellationToken cancellationToken)
    {
        if (kayit is not null)
        {
            kayit.Durum = EBelgeKaydiDurumu.UnsignedUblKaliciHata;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var failEdildi = await _leaseTransitionService.TryFailJobAsync(
            talep.OutboxMesajiId, talep.KurumId, talep.EBelgeKaydiId, EBelgeOutboxIsTuru.ArtefaktOlustur, talep.KilitToken, hataKodu, hataMesaji, retryDelay: null, cancellationToken);

        if (!failEdildi)
        {
            await tx.RollbackAsync(cancellationToken);
            return EBelgeArtefaktOlusturmaSonucu.SahiplikKaybedildi();
        }

        await tx.CommitAsync(cancellationToken);
        return EBelgeArtefaktOlusturmaSonucu.AtomikKaliciHata(hataKodu, hataMesaji);
    }

    /// <summary>
    /// Faz 2B.10 görev md.12 - üç koşulun HEPSİNİ kontrol eder: (1) immutable karar
    /// YerelImzaOlustur=true, (2) global signing aktivasyon kapısı açık, (3) GÜNCEL kurum
    /// politikası hâlâ aktif. Karar kaydı BULUNAMAZSA (legacy/karar-öncesi kayıt) fail-closed
    /// olarak `false` döner - imzalama mesajı OLUŞTURULMAZ (bkz. `EBelgeSigningBackfillService`,
    /// AYNI invariantı backfill İÇİN de uygular).
    /// </summary>
    private async Task<bool> ImzalamaMesajiOlusturulmaliMiAsync(EBelgeArtefaktOlusturmaTalebi talep, CancellationToken cancellationToken)
    {
        if (!_signingActivationGate.ShouldCreateSigningMessage())
        {
            return false;
        }

        var karar = await _dbContext.Set<SatisBelgesiEBelgeKarari>()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.EBelgeKaydiId == talep.EBelgeKaydiId && k.KurumId == talep.KurumId, cancellationToken);

        if (karar is null || !karar.YerelImzaOlustur)
        {
            return false;
        }

        var buguneTrt = TurkeyTimeZoneHelper.UtcdenTurkiyeYereleCevir(_timeProvider.GetUtcNow().UtcDateTime);
        var guncelKarar = await _kurumPolitikaServisi.DegerlendirAsync(talep.KurumId, buguneTrt, cancellationToken);
        return guncelKarar.Yetenekler.YerelImzaOlustur;
    }

    private static bool EslesiyorMu(EBelgeArtifact mevcutArtifact, EBelgeUblRenderSonucu renderSonuc, string snapshotHash) =>
        !mevcutArtifact.IsDeleted &&
        string.Equals(mevcutArtifact.KaynakSnapshotSha256, snapshotHash, StringComparison.Ordinal) &&
        string.Equals(mevcutArtifact.ArtifactSha256, renderSonuc.UnsignedUblSha256, StringComparison.Ordinal) &&
        string.Equals(mevcutArtifact.RuleSetId, renderSonuc.KuralSetiKimligi, StringComparison.Ordinal);

    private static bool IsBenzersizlikIhlali(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx &&
        sqlEx.Errors.Cast<SqlError>().Any(e => e.Number is SqlUniqueConstraintViolation or SqlUniqueIndexViolation);

    /// <summary>Kullanıcı girdisinden DOĞRUDAN dosya sistemi path'i türetilmez - yalnız [A-Za-z0-9_-] karakterlerine izin verilir, sabit ".xml" uzantısı eklenir.</summary>
    private static string TurentDosyaAdi(EBelgeCanonicalSnapshotV2 snapshot)
    {
        var kaynak = !string.IsNullOrWhiteSpace(snapshot.Belge.ResmiFaturaNo)
            ? snapshot.Belge.ResmiFaturaNo!
            : snapshot.Belge.EBelgeUuid;

        var temiz = GuvenliDosyaAdiKarakterleri.Replace(kaynak, string.Empty);
        if (string.IsNullOrEmpty(temiz))
        {
            temiz = "ebelge";
        }

        return temiz + ".xml";
    }

    /// <summary>
    /// Hata mesajları veritabanında saklanacağından (SonHataMesaji, en fazla 2000 karakter) ve
    /// loglanacağından, XML/kişisel veri İÇERMEYEN, ilgili exception'ın kendi güvenli/sınırlı
    /// mesajını kullanır (bu exception tipleri zaten güvenli mesaj sözleşmesine sahiptir - bkz.
    /// EBelgeUblRenderExceptions.cs, md.14). Lease token HİÇBİR YERDE loglanmaz.
    /// </summary>
    private static string GuvenliMesaj(Exception ex) => ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
}

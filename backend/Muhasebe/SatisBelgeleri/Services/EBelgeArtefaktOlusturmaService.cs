using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// <see cref="IEBelgeArtefaktOlusturmaService"/>'in GERÇEK implementasyonu - immutable
/// EBelgeKaydi/EBelgeSnapshot kaydından V2 canonical snapshot'ı okur, GERÇEK
/// <see cref="IEBelgeUblRenderer"/>'ı (yerel XSD + gerçek Java Saxon sidecar Schematron
/// doğrulaması dahil) çağırır ve başarılı sonucu immutable <see cref="EBelgeArtifact"/> olarak
/// kalıcılaştırır (bkz. Faz 2B.6 görev md.6-8).
///
/// Üç aşamalı akış (md.7): (1) kısa okuma - snapshot + mevcut artefakt kontrolü, AÇIK transaction
/// YOKTUR; (2) DB DIŞI render (sidecar HTTP çağrısı dahil) - hiçbir satır kilidi TUTULMAZ; (3)
/// kısa yazma - artefakt insert + EBelgeKaydi.Durum güncellemesi TEK SaveChangesAsync'te (tek
/// implicit transaction). Outbox TAMAMLAMA geçişi (TryCompleteAsync) bu servisin DIŞINDA, ayrı
/// bir lease-token-korumalı çağrıdır (mevcut mimari - bkz. EBelgeOutboxMesajIslemeService); bu
/// servis o adımı bilerek İÇERMEZ. Bu ayrımın güvenliği İDEMPOTENCY ile sağlanır: artefakt zaten
/// varsa (ikinci worker veya tamamlama başarısız olup mesaj yeniden işlendiyse) yeniden insert
/// DENENMEZ, aynı sonuç (Basarili) döner - bkz. görev md.6 son cümle.
/// </summary>
public sealed class EBelgeArtefaktOlusturmaService : IEBelgeArtefaktOlusturmaService
{
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    private static readonly Regex GuvenliDosyaAdiKarakterleri = new("[^A-Za-z0-9_-]", RegexOptions.Compiled);

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeCanonicalSnapshotV2Reader _snapshotReader;
    private readonly IEBelgeUblRenderer _renderer;
    private readonly ILogger<EBelgeArtefaktOlusturmaService> _logger;

    public EBelgeArtefaktOlusturmaService(
        StysAppDbContext dbContext,
        IEBelgeCanonicalSnapshotV2Reader snapshotReader,
        IEBelgeUblRenderer renderer,
        ILogger<EBelgeArtefaktOlusturmaService> logger)
    {
        _dbContext = dbContext;
        _snapshotReader = snapshotReader;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<EBelgeArtefaktOlusturmaSonucu?> OlusturAsync(
        EBelgeArtefaktOlusturmaTalebi talep,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(talep);

        // ---- Faz 1: kısa okuma (açık transaction yok) ----
        var kayit = await _dbContext.Set<EBelgeKaydi>()
            .Include(x => x.Snapshot)
            .FirstOrDefaultAsync(x => x.Id == talep.EBelgeKaydiId && x.KurumId == talep.KurumId, cancellationToken);

        if (kayit is null)
        {
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                "EBELGE_KAYDI_BULUNAMADI", "E-belge kaydı bulunamadı.");
        }

        if (kayit.Snapshot is null)
        {
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                "EBELGE_SNAPSHOT_BULUNAMADI", "E-belge kaydına ait canonical snapshot bulunamadı.");
        }

        var mevcutArtifact = await _dbContext.Set<EBelgeArtifact>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.KurumId == talep.KurumId &&
                a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned,
                cancellationToken);

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
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                EBelgeCanonicalSnapshotException.SafeErrorCode, EBelgeCanonicalSnapshotException.SafeMessage);
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
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(EBelgeUblRenderSnapshotVersionUnsupportedException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblRenderScopeUnsupportedException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(EBelgeUblRenderScopeUnsupportedException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblAuthoritativeFieldMissingException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(EBelgeUblAuthoritativeFieldMissingException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblMonetaryTotalMismatchException)
        {
            // Snapshot immutable olduğundan AYNI mesajı yeniden denemek sorunu ÇÖZMEZ - kalıcı
            // düzeltilebilir iş hatası: belge verisi düzeltilip YENİ bir fatura/snapshot/outbox
            // mesajı üretilmelidir (bkz. görev md.10, "düzeltilebilir iş hataları").
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                EBelgeUblMonetaryTotalMismatchException.SafeErrorCode,
                "Belge toplamları tutarsız - snapshot immutable olduğundan bu mesaj tekrar denenerek çözülemez; belge düzeltilip yeni bir kesim/snapshot üretilmelidir.");
        }
        catch (EBelgeUblXsdValidationFailedException ex)
        {
            // Hata mesajı METNİ YAZILMAZ - XSD hata metinleri XML'den alınan alan DEĞERLERİNİ
            // (ör. ProfileID) echo edebilir; yalnız sayı ve sabit şablon saklanır (bkz. md.14).
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                EBelgeUblXsdValidationFailedException.SafeErrorCode,
                $"XSD doğrulaması {ex.Hatalar.Count} hata ile başarısız oldu.");
        }
        catch (EBelgeUblSchematronValidationFailedException ex)
        {
            // Aynı gerekçe: schematron ihlal metinleri alan DEĞERLERİNİ echo edebilir (bkz.
            // sidecar SVRL çıktısı) - yalnız ihlal SAYISI saklanır, ham metin YOK.
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
                EBelgeUblSchematronValidationFailedException.SafeErrorCode,
                $"Schematron doğrulaması {ex.Ihlaller.Count} ihlal ile başarısız oldu.");
        }
        catch (EBelgeUblRuleSetArtifactInvalidException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.KaliciHata(EBelgeUblRuleSetArtifactInvalidException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblSchematronServiceUnavailableException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.GeciciHata(EBelgeUblSchematronServiceUnavailableException.SafeErrorCode, GuvenliMesaj(ex));
        }
        catch (EBelgeUblSchematronProtocolErrorException ex)
        {
            return EBelgeArtefaktOlusturmaSonucu.GeciciHata(EBelgeUblSchematronProtocolErrorException.SafeErrorCode, GuvenliMesaj(ex));
        }

        // ---- Faz 3: kısa yazma ----
        if (mevcutArtifact is not null)
        {
            return DegerlendirIdempotentSonuc(mevcutArtifact, renderSonuc, snapshotHash);
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
            Icerik = renderSonuc.UnsignedUblUtf8.ToArray(),
            MimeType = "application/xml",
            DosyaAdi = TurentDosyaAdi(snapshot),
            OlusturulmaZamaniUtc = DateTime.UtcNow,
        };

        _dbContext.Add(yeniArtifact);
        kayit.Durum = EBelgeKaydiDurumu.UnsignedUblHazir;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsBenzersizlikIhlali(ex))
        {
            // Yarış durumu: başka bir worker aynı artefaktı bu sırada eklemiş olabilir (bkz.
            // görev md.12, "iki paralel worker tek artifact üretir"). Yerel değişikliği geri al,
            // rakip satırı OKU ve idempotency karşılaştırmasıyla sonuçlandır.
            _dbContext.Entry(yeniArtifact).State = EntityState.Detached;
            _dbContext.Entry(kayit).Reload();

            var rakipArtifact = await _dbContext.Set<EBelgeArtifact>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.KurumId == talep.KurumId &&
                    a.EBelgeKaydiId == talep.EBelgeKaydiId &&
                    a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                    a.ArtifactAsamasi == EBelgeArtifactAsamasi.Unsigned,
                    cancellationToken);

            if (rakipArtifact is null)
            {
                _logger.LogWarning(ex, "Benzersizlik ihlali sonrası rakip artefakt bulunamadı - geçici olarak sınıflandırılıyor.");
                return EBelgeArtefaktOlusturmaSonucu.GeciciHata(
                    "EBELGE_ARTIFACT_YARIS_DURUMU", "Artefakt eşzamanlı yazma çakışması - yeniden denenmeli.");
            }

            return DegerlendirIdempotentSonuc(rakipArtifact, renderSonuc, snapshotHash);
        }

        return EBelgeArtefaktOlusturmaSonucu.Basarili();
    }

    private static EBelgeArtefaktOlusturmaSonucu DegerlendirIdempotentSonuc(
        EBelgeArtifact mevcutArtifact,
        EBelgeUblRenderSonucu renderSonuc,
        string snapshotHash)
    {
        var ayniZincir =
            string.Equals(mevcutArtifact.KaynakSnapshotSha256, snapshotHash, StringComparison.Ordinal) &&
            string.Equals(mevcutArtifact.ArtifactSha256, renderSonuc.UnsignedUblSha256, StringComparison.Ordinal) &&
            string.Equals(mevcutArtifact.RuleSetId, renderSonuc.KuralSetiKimligi, StringComparison.Ordinal);

        if (ayniZincir)
        {
            return EBelgeArtefaktOlusturmaSonucu.Basarili();
        }

        return EBelgeArtefaktOlusturmaSonucu.KaliciHata(
            EBelgeArtifactIdempotencyConflictException.SafeErrorCode,
            "Aynı benzersiz anahtar altında farklı içerikli bir artefakt zaten mevcut.");
    }

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
    /// EBelgeUblRenderExceptions.cs, md.14).
    /// </summary>
    private static string GuvenliMesaj(Exception ex) => ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
}

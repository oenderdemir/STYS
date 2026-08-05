using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// `UnsignedUblHazir` durumundaki, henüz bir `UblImzala` outbox mesajı ALMAMIŞ (SignedReady
/// artefaktı da OLMAYAN) e-belge kayıtları için, KONTROLLÜ ve İDEMPOTENT bir şekilde imzalama
/// outbox mesajı oluşturur (bkz. Faz 2B.7.1 görev md.7). Bu servis, aktivasyon tarihinden ÖNCE
/// oluşturulmuş (bu yüzden `EBelgeArtefaktOlusturmaService`'in "yalnız İLK GERÇEK oluşturmada
/// mesaj ekle" kuralı gereği kendiliğinden kuyruğa GİRMEMİŞ) kayıtları, aktivasyon SONRASI
/// telafi etmek İÇİNDİR.
///
/// KASITLI OLARAK YAPILMAYANLAR (bkz. görev md.7):
/// - Otomatik/sürekli çalışan bir hosted worker/`BackgroundService` EKLENMEZ.
/// - Bir HTTP API endpoint'i EKLENMEZ.
/// - Production'da KENDİLİĞİNDEN çağrılmaz - operasyonel tetikleme (manuel/gelecekteki bir admin
///   aracı üzerinden) SONRAKİ bir faza bırakılmıştır; bu tur yalnız uygulama servisini VE
///   testlerini ekler.
///
/// Aktivasyon kapısına (`IEBelgeSigningActivationGate`) TABİDİR - kapı KAPALIYKEN backfill de
/// (canlı akışla TUTARLI biçimde) YENİ imzalama mesajı OLUŞTURMAZ; 15 Eylül 2026 öncesi
/// production imzalamasının YAPISAL olarak engellendiği invariant'ı backfill için de GEÇERLİDİR.
/// </summary>
public interface IEBelgeSigningBackfillService
{
    /// <summary>Belirtilen kurum sınırındaki uygun kayıtlar için idempotent olarak UblImzala outbox mesajı oluşturur ve OLUŞTURULAN (önceden var olmayan) mesaj sayısını döner.</summary>
    Task<int> BackfillAsync(int kurumId, CancellationToken cancellationToken = default);
}

public sealed class EBelgeSigningBackfillService : IEBelgeSigningBackfillService
{
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    private readonly StysAppDbContext _dbContext;
    private readonly IEBelgeSigningActivationGate _activationGate;
    private readonly ILogger<EBelgeSigningBackfillService> _logger;

    public EBelgeSigningBackfillService(
        StysAppDbContext dbContext,
        IEBelgeSigningActivationGate activationGate,
        ILogger<EBelgeSigningBackfillService> logger)
    {
        _dbContext = dbContext;
        _activationGate = activationGate;
        _logger = logger;
    }

    public async Task<int> BackfillAsync(int kurumId, CancellationToken cancellationToken = default)
    {
        if (kurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        // Canlı akışla (EBelgeArtefaktOlusturmaService) TUTARLI aktivasyon politikası - bkz.
        // sınıf düzeyi XML doc'u.
        if (!_activationGate.ShouldCreateSigningMessage())
        {
            return 0;
        }

        // Adaylar: Durum=UnsignedUblHazir, SignedReady artefaktı YOK, UblImzala outbox mesajı YOK
        // - kurum sınırında (bkz. görev md.7).
        var adayEBelgeKaydiIdleri = await _dbContext.Set<EBelgeKaydi>()
            .Where(k => k.KurumId == kurumId && k.Durum == EBelgeKaydiDurumu.UnsignedUblHazir)
            .Where(k => !_dbContext.Set<EBelgeArtifact>().IgnoreQueryFilters().Any(a =>
                a.KurumId == kurumId &&
                a.EBelgeKaydiId == k.Id &&
                a.ArtifactTipi == EBelgeArtifactTipi.UblXml &&
                a.ArtifactAsamasi == EBelgeArtifactAsamasi.SignedReady))
            .Where(k => !_dbContext.Set<EBelgeOutboxMesaji>().Any(o =>
                o.KurumId == kurumId &&
                o.EBelgeKaydiId == k.Id &&
                o.IsTuru == EBelgeOutboxIsTuru.UblImzala))
            .Select(k => k.Id)
            .ToListAsync(cancellationToken);

        var olusturulanSayisi = 0;
        foreach (var eBelgeKaydiId in adayEBelgeKaydiIdleri)
        {
            if (await OlusturTekMesajIdempotentAsync(kurumId, eBelgeKaydiId, cancellationToken))
            {
                olusturulanSayisi++;
            }
        }

        return olusturulanSayisi;
    }

    /// <summary>
    /// Tek bir EBelgeKaydi için UblImzala outbox mesajını İDEMPOTENT olarak ekler - eşzamanlı,
    /// AYNI (EBelgeKaydiId, IsTuru) benzersiz anahtarına yazmaya çalışan başka bir işlem (ör.
    /// canlı akışın kendisi ya da backfill'in eşzamanlı BAŞKA bir çağrısı) İLE yarışırsa unique
    /// -violation'ı sessizce yutar ve `false` döner - bu, "mesaj zaten var" anlamına gelir,
    /// GERÇEK bir hata DEĞİLDİR.
    /// </summary>
    private async Task<bool> OlusturTekMesajIdempotentAsync(int kurumId, int eBelgeKaydiId, CancellationToken cancellationToken)
    {
        _dbContext.Add(new EBelgeOutboxMesaji
        {
            KurumId = kurumId,
            EBelgeKaydiId = eBelgeKaydiId,
            IsTuru = EBelgeOutboxIsTuru.UblImzala,
            Durum = EBelgeOutboxDurumu.Bekliyor,
            DenemeSayisi = 0,
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsBenzersizlikIhlali(ex))
        {
            _dbContext.ChangeTracker.Clear();
            _logger.LogInformation(ex, "Backfill: UblImzala outbox mesajı zaten mevcut, atlandı (EBelgeKaydiId={EBelgeKaydiId}).", eBelgeKaydiId);
            return false;
        }
    }

    private static bool IsBenzersizlikIhlali(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx &&
        sqlEx.Errors.Cast<SqlError>().Any(e => e.Number is SqlUniqueConstraintViolation or SqlUniqueIndexViolation);
}

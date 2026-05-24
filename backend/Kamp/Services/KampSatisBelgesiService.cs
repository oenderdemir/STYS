using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

/// <summary>
/// Kamp rezervasyon verisinden ortak satış belgesi taslağı oluşturma servisi.
/// Kamp modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// rezervasyon verisini iletir.
///
/// DbContext doğrudan kullanılır — KampBasvuru ve KampDonemi navigation'ları
/// Include ile birlikte tek seferde çekilmelidir; ayrıca KDV istisna tanımları
/// AsNoTracking ile okunur.
/// </summary>
public class KampSatisBelgesiService : IKampSatisBelgesiService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly ILogger<KampSatisBelgesiService> _logger;

    private const string KaynakTipiKampRezervasyon = "KampRezervasyon";
    private const decimal VarsayilanKdvOrani = 10m;

    public KampSatisBelgesiService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        ILogger<KampSatisBelgesiService> logger)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        KampSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Route ID ile body ID eşleşmeli
        if (rezervasyonId != request.RezervasyonId)
        {
            throw new BaseException("Rezervasyon ID uyuşmazlığı: route ve body farklı.", 400);
        }

        // 2. Rezervasyonu bul (KampBasvuru ve KampDonemi navigation'ları dahil) ve access scope kontrolü yap
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);

        // 3. Durum validasyonu: yalnızca Aktif rezervasyonlar için taslak oluşturulabilir
        ValidateRezervasyonDurumu(rezervasyon);

        // 4. Toplam tutar validasyonu
        if (rezervasyon.DonemToplamTutar <= 0)
        {
            throw new BaseException(
                "Rezervasyon dönem toplam tutarı bulunamadığı için satış belgesi taslağı oluşturulamaz.",
                400);
        }

        // 5. Müşteri bilgilerini çözümle
        var musteriBilgi = ResolveMusteriBilgileri(request, rezervasyon);

        // 6. Satış satırını oluştur (KampHizmeti, tek satır)
        var satirlar = await BuildSatirlarAsync(rezervasyon, request, cancellationToken);

        // 7. Belge tarihi ve açıklama (request öncelikli)
        var belgeTarihi = request.BelgeTarihi ?? DateTime.Today;
        var aciklama = request.Aciklama
            ?? $"Kamp rezervasyonu: {rezervasyon.RezervasyonNo}";

        // 8. Taslak request oluştur
        var taslakRequest = new SatisBelgesiTaslakOlusturRequest
        {
            KaynakModul = SatisKaynakModulu.Kamp,
            KaynakTipi = KaynakTipiKampRezervasyon,
            KaynakId = rezervasyonId.ToString(),
            TesisId = rezervasyon.TesisId,
            BelgeTarihi = belgeTarihi,
            VadeTarihi = request.VadeTarihi,
            KurumsalMi = musteriBilgi.kurumsalMi,
            MusteriUnvan = musteriBilgi.musteriUnvan,
            MusteriAdSoyad = musteriBilgi.musteriAdSoyad,
            MusteriVergiNo = musteriBilgi.musteriVergiNo,
            MusteriTcKimlikNo = musteriBilgi.musteriTcKimlikNo,
            MusteriVergiDairesi = request.MusteriVergiDairesi,
            MusteriAdres = request.MusteriAdres,
            MusteriEposta = request.MusteriEposta,
            MusteriTelefon = request.MusteriTelefon,
            Aciklama = aciklama,
            Satirlar = satirlar
        };

        // 9. ISatisBelgesiTaslakOlusturmaService'e ilet
        _logger.LogInformation(
            "Kamp rezervasyonu #{RezervasyonId} ({RezervasyonNo}) için satış belgesi taslağı oluşturuluyor. Tutar: {Tutar}, Kurumsal: {KurumsalMi}",
            rezervasyonId, rezervasyon.RezervasyonNo, rezervasyon.DonemToplamTutar, musteriBilgi.kurumsalMi);

        var result = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(taslakRequest, cancellationToken);

        _logger.LogInformation(
            "Kamp rezervasyonu #{RezervasyonId} için satış belgesi taslağı oluşturuldu. BelgeId: {BelgeId}, BelgeNo: {BelgeNo}",
            rezervasyonId, result.Id, result.BelgeNo);

        return result;
    }

    // ──────────────────────────────────────────────
    //  Private — Rezervasyon bulma ve access scope
    // ──────────────────────────────────────────────

    private async Task<KampRezervasyon> GetScopedRezervasyonAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Geçersiz rezervasyon ID.", 400);
        }

        // DbContext doğrudan kullanılır — KampBasvuru ve KampDonemi navigation'ları
        // Include ile birlikte tek seferde çekilmelidir.
        var rezervasyon = await _dbContext.KampRezervasyonlari
            .Include(x => x.KampBasvuru)
            .Include(x => x.KampDonemi)
            .FirstOrDefaultAsync(x => x.Id == rezervasyonId, cancellationToken);

        if (rezervasyon is null)
        {
            throw new BaseException("Kamp rezervasyonu bulunamadı.", 404);
        }

        // Access scope kontrolü: KampRezervasyon → TesisId (direct)
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(rezervasyon.TesisId))
        {
            throw new BaseException("Bu rezervasyon için yetkiniz bulunmuyor.", 403);
        }

        return rezervasyon;
    }

    // ──────────────────────────────────────────────
    //  Private — Durum validasyonu
    // ──────────────────────────────────────────────

    private static void ValidateRezervasyonDurumu(KampRezervasyon rezervasyon)
    {
        if (rezervasyon.Durum == KampRezervasyonDurumlari.IptalEdildi)
        {
            throw new BaseException("İptal edilen rezervasyon için satış belgesi taslağı oluşturulamaz.", 400);
        }

        if (rezervasyon.Durum != KampRezervasyonDurumlari.Aktif)
        {
            throw new BaseException(
                $"Satış belgesi taslağı yalnızca aktif rezervasyonlar için oluşturulabilir. Mevcut durum: {rezervasyon.Durum}",
                400);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Satış satırını oluşturma
    //             Kamp rezervasyonu tek satır olarak
    //             KampHizmeti tipinde oluşturulur.
    // ──────────────────────────────────────────────

    private async Task<List<SatisBelgesiTaslakSatirRequest>> BuildSatirlarAsync(
        KampRezervasyon rezervasyon,
        KampSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken)
    {
        // KDV parametrelerini çözümle
        KdvUygulamaTipi kdvUygulamaTipi;
        decimal kdvOrani;
        int? kdvIstisnaTanimId = null;

        if (request.KdvIstisnaTanimId.HasValue && request.KdvIstisnaTanimId.Value > 0)
        {
            var istisna = await _dbContext.KdvIstisnaTanimlari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.KdvIstisnaTanimId.Value, cancellationToken);

            if (istisna is null)
            {
                throw new BaseException(
                    $"KDV istisna tanımı bulunamadı (Id: {request.KdvIstisnaTanimId.Value}).",
                    400);
            }

            kdvUygulamaTipi = istisna.UygulamaTipi;
            kdvOrani = 0m;
            kdvIstisnaTanimId = istisna.Id;
        }
        else
        {
            // Kamp rezervasyonunda ürün/konaklama bazlı KDV oranı bulunmadığı için
            // request/default oran kullanılır.
            kdvUygulamaTipi = KdvUygulamaTipi.Kdvli;
            kdvOrani = request.KdvOrani ?? VarsayilanKdvOrani;
        }

        // KampHizmeti — tek satır, Miktar = 1, BirimFiyat = DonemToplamTutar
        var satirlar = new List<SatisBelgesiTaslakSatirRequest>
        {
            new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.KampHizmeti,
                Aciklama = $"Kamp rezervasyonu: {rezervasyon.RezervasyonNo}",
                Miktar = 1,
                BirimFiyat = rezervasyon.DonemToplamTutar,
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvOrani = kdvOrani,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KaynakSatirId = rezervasyon.Id.ToString()
            }
        };

        return satirlar;
    }

    // ──────────────────────────────────────────────
    //  Private — Müşteri bilgilerini çözümleme
    //             Kamp rezervasyonunda BasvuruSahibiAdiSoyadi
    //             bireysel fatura için fallback olarak kullanılır.
    //             Kurumsal fatura için unvan/vergi no request'ten zorunludur.
    // ──────────────────────────────────────────────

    private static (
        bool kurumsalMi,
        string? musteriUnvan,
        string? musteriAdSoyad,
        string? musteriVergiNo,
        string? musteriTcKimlikNo)
        ResolveMusteriBilgileri(KampSatisBelgesiTaslakRequest request, KampRezervasyon rezervasyon)
    {
        if (request.KurumsalMi)
        {
            // ── Kurumsal fatura ──
            if (string.IsNullOrWhiteSpace(request.MusteriUnvan))
            {
                throw new BaseException("Kurumsal fatura için müşteri ünvanı zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(request.MusteriVergiNo))
            {
                throw new BaseException("Kurumsal fatura için vergi numarası zorunludur.", 400);
            }

            return (
                kurumsalMi: true,
                musteriUnvan: request.MusteriUnvan,
                musteriAdSoyad: null,
                musteriVergiNo: request.MusteriVergiNo,
                musteriTcKimlikNo: request.MusteriTcKimlikNo
            );
        }
        else
        {
            // ── Bireysel fatura ──
            // Request'teki MusteriAdSoyad önceliklidir; boşsa rezervasyondaki
            // BasvuruSahibiAdiSoyadi fallback olarak kullanılır.
            var adSoyad = request.MusteriAdSoyad ?? rezervasyon.BasvuruSahibiAdiSoyadi;

            if (string.IsNullOrWhiteSpace(adSoyad))
            {
                throw new BaseException("Bireysel fatura için müşteri ad soyad zorunludur.", 400);
            }

            return (
                kurumsalMi: false,
                musteriUnvan: null,
                musteriAdSoyad: adSoyad,
                musteriVergiNo: null,
                musteriTcKimlikNo: request.MusteriTcKimlikNo
            );
        }
    }
}

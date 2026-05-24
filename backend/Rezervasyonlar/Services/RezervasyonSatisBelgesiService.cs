using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon check-out verisinden ortak satış belgesi taslağı oluşturma servisi.
/// Otel modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// rezervasyon verisini iletir.
/// </summary>
public class RezervasyonSatisBelgesiService : IRezervasyonSatisBelgesiService
{
    private readonly Infrastructure.EntityFramework.StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly ILogger<RezervasyonSatisBelgesiService> _logger;

    private const string KaynakTipiRezervasyonCheckout = "RezervasyonCheckout";
    private const decimal VarsayilanKdvOrani = 10m;

    public RezervasyonSatisBelgesiService(
        Infrastructure.EntityFramework.StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        ILogger<RezervasyonSatisBelgesiService> logger)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int rezervasyonId,
        RezervasyonSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Route ID ile body ID eşleşmeli
        if (rezervasyonId != request.RezervasyonId)
        {
            throw new BaseException("Rezervasyon ID uyuşmazlığı: route ve body farklı.", 400);
        }

        // 2. Rezervasyonu bul ve access scope kontrolü yap
        var rezervasyon = await GetScopedRezervasyonAsync(rezervasyonId, cancellationToken);

        // 3. Durum validasyonu: yalnızca CheckOutTamamlandi rezervasyonlar için taslak oluşturulabilir
        ValidateRezervasyonDurumu(rezervasyon);

        // 4. Gece sayısı hesapla
        var geceSayisi = CalculateNightCount(rezervasyon.GirisTarihi, rezervasyon.CikisTarihi);
        if (geceSayisi <= 0)
        {
            throw new BaseException("Rezervasyon gece sayısı hesaplanamadı.", 400);
        }

        // 5. Satış satırlarını oluştur (her gece için bir satır)
        var satirlar = BuildSatirlar(rezervasyon, geceSayisi);

        // 6. Müşteri bilgilerini çözümle
        var (kurumsalMi, musteriUnvan, musteriAdSoyad, musteriVergiNo, musteriTcKimlikNo) =
            ResolveMusteriBilgileri(rezervasyon);

        // 7. Taslak request oluştur
        var taslakRequest = new SatisBelgesiTaslakOlusturRequest
        {
            KaynakModul = SatisKaynakModulu.Otel,
            KaynakTipi = KaynakTipiRezervasyonCheckout,
            KaynakId = rezervasyonId.ToString(),
            TesisId = rezervasyon.TesisId,
            BelgeTarihi = rezervasyon.CikisTarihi.Date,
            VadeTarihi = null,
            KurumsalMi = kurumsalMi,
            MusteriUnvan = musteriUnvan,
            MusteriAdSoyad = musteriAdSoyad,
            MusteriVergiNo = musteriVergiNo,
            MusteriTcKimlikNo = musteriTcKimlikNo,
            MusteriVergiDairesi = null,
            MusteriAdres = null,
            MusteriEposta = rezervasyon.MisafirEposta,
            MusteriTelefon = rezervasyon.MisafirTelefon,
            Aciklama = $"Check-out: {rezervasyon.ReferansNo} — {rezervasyon.MisafirAdiSoyadi}",
            Satirlar = satirlar
        };

        // 8. ISatisBelgesiTaslakOlusturmaService'e ilet
        _logger.LogInformation(
            "Rezervasyon #{RezervasyonId} için satış belgesi taslağı oluşturuluyor. Gece sayısı: {GeceSayisi}, Toplam ücret: {ToplamUcret}",
            rezervasyonId, geceSayisi, rezervasyon.ToplamUcret);

        var result = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(taslakRequest, cancellationToken);

        _logger.LogInformation(
            "Rezervasyon #{RezervasyonId} için satış belgesi taslağı oluşturuldu. BelgeId: {BelgeId}, BelgeNo: {BelgeNo}",
            rezervasyonId, result.Id, result.BelgeNo);

        return result;
    }

    // ──────────────────────────────────────────────
    //  Private — Rezervasyon bulma ve access scope
    // ──────────────────────────────────────────────

    private async Task<Rezervasyon> GetScopedRezervasyonAsync(int rezervasyonId, CancellationToken cancellationToken)
    {
        if (rezervasyonId <= 0)
        {
            throw new BaseException("Geçersiz rezervasyon ID.", 400);
        }

        var rezervasyon = await _dbContext.Rezervasyonlar
            .FirstOrDefaultAsync(x => x.Id == rezervasyonId, cancellationToken);

        if (rezervasyon is null)
        {
            throw new BaseException("Rezervasyon bulunamadı.", 404);
        }

        // Access scope kontrolü (RezervasyonService.GetScopedReservationForManageAsync ile aynı pattern)
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

    private static void ValidateRezervasyonDurumu(Rezervasyon rezervasyon)
    {
        if (rezervasyon.RezervasyonDurumu == RezervasyonDurumlari.Iptal)
        {
            throw new BaseException("İptal edilen rezervasyon için satış belgesi taslağı oluşturulamaz.", 400);
        }

        if (rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.CheckOutTamamlandi)
        {
            throw new BaseException(
                $"Satış belgesi taslağı yalnızca check-out tamamlanmış rezervasyonlar için oluşturulabilir. Mevcut durum: {rezervasyon.RezervasyonDurumu}",
                400);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Gece sayısı hesaplama
    // ──────────────────────────────────────────────

    private static int CalculateNightCount(DateTime girisTarihi, DateTime cikisTarihi)
    {
        return Math.Max(0, (cikisTarihi.Date - girisTarihi.Date).Days);
    }

    // ──────────────────────────────────────────────
    //  Private — Satış satırlarını oluşturma
    // ──────────────────────────────────────────────

    private static List<SatisBelgesiTaslakSatirRequest> BuildSatirlar(Rezervasyon rezervasyon, int geceSayisi)
    {
        var birimFiyat = geceSayisi > 0
            ? Math.Round(rezervasyon.ToplamUcret / geceSayisi, 2, MidpointRounding.AwayFromZero)
            : rezervasyon.ToplamUcret;

        // Son satırda kuruş yuvarlama farkını dengele
        // (toplam = birimFiyat * geceSayisi her zaman tam eşit olmayabilir)
        var toplamDagitilan = birimFiyat * geceSayisi;
        var fark = rezervasyon.ToplamUcret - toplamDagitilan;

        var satirlar = new List<SatisBelgesiTaslakSatirRequest>(geceSayisi);
        for (var i = 0; i < geceSayisi; i++)
        {
            var geceTarihi = rezervasyon.GirisTarihi.Date.AddDays(i);
            var satirBirimFiyat = birimFiyat;

            // Son satıra yuvarlama farkını ekle
            if (i == geceSayisi - 1 && fark != 0)
            {
                satirBirimFiyat += fark;
            }

            satirlar.Add(new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.Konaklama,
                Aciklama = $"Konaklama — {geceTarihi:dd.MM.yyyy}",
                Miktar = 1,
                BirimFiyat = satirBirimFiyat,
                KdvUygulamaTipi = KdvUygulamaTipi.Kdvli,
                KdvOrani = VarsayilanKdvOrani,
                KaynakSatirId = $"{rezervasyon.Id}_{geceTarihi:yyyyMMdd}"
            });
        }

        return satirlar;
    }

    // ──────────────────────────────────────────────
    //  Private — Müşteri bilgilerini çözümleme
    // ──────────────────────────────────────────────

    /// <summary>
    /// Rezervasyondaki misafir bilgilerinden kurumsal/bireysel müşteri alanlarını çözümler.
    /// Rezervasyonlar her zaman bireysel müşteri olarak işlenir.
    /// </summary>
    private static (
        bool kurumsalMi,
        string? musteriUnvan,
        string? musteriAdSoyad,
        string? musteriVergiNo,
        string? musteriTcKimlikNo)
        ResolveMusteriBilgileri(Rezervasyon rezervasyon)
    {
        // Rezervasyonlar her zaman bireyseldir (kurumsal fatura için ayrı bir flow gerekir).
        // TcKimlikNo varsa vergi kimlik numarası yerine TC kimlik numarası kullanılır.
        return (
            kurumsalMi: false,
            musteriUnvan: null,
            musteriAdSoyad: rezervasyon.MisafirAdiSoyadi,
            musteriVergiNo: null,
            musteriTcKimlikNo: rezervasyon.TcKimlikNo
        );
    }
}

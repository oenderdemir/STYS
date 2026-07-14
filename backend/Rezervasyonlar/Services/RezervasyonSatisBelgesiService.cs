using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
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
    private readonly IRezervasyonCariKartResolver _cariKartResolver;
    private readonly ILogger<RezervasyonSatisBelgesiService> _logger;

    private const string KaynakTipiRezervasyonCheckout = "RezervasyonCheckout";

    /// <summary>Ek hizmet ve restoran (OdayaEkle) satırlarında kullanılan varsayılan KDV oranı.
    /// BİLİNEN SINIRLAMA: ne RezervasyonEkHizmet ne de restoran siparişi (OdayaEkle) satır bazında
    /// KDV kırılımı taşıyor — gerçek oran bundan farklıysa (örn. alkollü içecek) bu bir veri
    /// doğruluğu açığıdır. Kapsamlı çözüm ilgili modüllerin KDV bilgisi taşımasını gerektirir.</summary>
    private const decimal VarsayilanKdvOrani = 10m;

    public RezervasyonSatisBelgesiService(
        Infrastructure.EntityFramework.StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        IRezervasyonCariKartResolver cariKartResolver,
        ILogger<RezervasyonSatisBelgesiService> logger)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _cariKartResolver = cariKartResolver;
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

        // 5. Toplam ücret validasyonu (gece sayısından ayrı)
        if (rezervasyon.ToplamUcret <= 0)
        {
            throw new BaseException(
                "Rezervasyon toplam tutarı bulunamadığı için satış belgesi taslağı oluşturulamaz.",
                400);
        }

        // 6. Satış satırlarını oluştur (konaklama + ek hizmet + restoran, KDV override bilgileriyle)
        var satirlar = await BuildSatirlarAsync(rezervasyon, geceSayisi, request, cancellationToken);

        // 6b. Cari kart çözümle (Faz 1'deki tahsilat kademesiyle aynı resolver — asla otomatik oluşturmaz)
        var cariKartId = await _cariKartResolver.ResolveAsync(rezervasyon, request.CariKartIdOverride, cancellationToken);

        // 7. Müşteri bilgilerini çözümle (request öncelikli, rezervasyon fallback)
        var musteriBilgi = ResolveMusteriBilgileri(request, rezervasyon);

        // 8. Belge tarihi ve açıklama (request öncelikli)
        var belgeTarihi = request.BelgeTarihi ?? rezervasyon.CikisTarihi.Date;
        var aciklama = request.Aciklama
            ?? $"Check-out: {rezervasyon.ReferansNo} — {rezervasyon.MisafirAdiSoyadi}";

        // 9. E‑posta / telefon (request öncelikli, rezervasyon fallback)
        var eposta = !string.IsNullOrWhiteSpace(request.MusteriEposta)
            ? request.MusteriEposta
            : rezervasyon.MisafirEposta;
        var telefon = !string.IsNullOrWhiteSpace(request.MusteriTelefon)
            ? request.MusteriTelefon
            : rezervasyon.MisafirTelefon;

        // 10. Taslak request oluştur
        var taslakRequest = new SatisBelgesiTaslakOlusturRequest
        {
            KaynakModul = SatisKaynakModulu.Otel,
            KaynakTipi = KaynakTipiRezervasyonCheckout,
            KaynakId = rezervasyonId.ToString(),
            TesisId = rezervasyon.TesisId,
            CariKartId = cariKartId,
            BelgeTarihi = belgeTarihi,
            VadeTarihi = request.VadeTarihi,
            KurumsalMi = musteriBilgi.kurumsalMi,
            MusteriUnvan = musteriBilgi.musteriUnvan,
            MusteriAdSoyad = musteriBilgi.musteriAdSoyad,
            MusteriVergiNo = musteriBilgi.musteriVergiNo,
            MusteriTcKimlikNo = musteriBilgi.musteriTcKimlikNo,
            MusteriVergiDairesi = request.MusteriVergiDairesi,
            MusteriAdres = request.MusteriAdres,
            MusteriEposta = eposta,
            MusteriTelefon = telefon,
            Aciklama = aciklama,
            Satirlar = satirlar
        };

        // 11. ISatisBelgesiTaslakOlusturmaService'e ilet
        _logger.LogInformation(
            "Rezervasyon #{RezervasyonId} için satış belgesi taslağı oluşturuluyor. Gece sayısı: {GeceSayisi}, Toplam ücret: {ToplamUcret}, Kurumsal: {KurumsalMi}",
            rezervasyonId, geceSayisi, rezervasyon.ToplamUcret, musteriBilgi.kurumsalMi);

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
    //  Private — Satış satırlarını oluşturma (KDV override desteğiyle)
    // ──────────────────────────────────────────────

    private async Task<List<SatisBelgesiTaslakSatirRequest>> BuildSatirlarAsync(
        Rezervasyon rezervasyon,
        int geceSayisi,
        RezervasyonSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken)
    {
        // KDV parametrelerini çözümle
        KdvUygulamaTipi kdvUygulamaTipi;
        decimal kdvOrani;
        int? kdvIstisnaTanimId = null;

        if (request.KdvIstisnaTanimId.HasValue && request.KdvIstisnaTanimId.Value > 0)
        {
            // İstisna tanımını oku (nihai validasyon SatisBelgesiService.CreateAsync'te yapılacak)
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
            kdvUygulamaTipi = KdvUygulamaTipi.Kdvli;
            kdvOrani = request.KdvOrani ?? VarsayilanKdvOrani;
        }

        var birimFiyat = geceSayisi > 0
            ? Math.Round(rezervasyon.ToplamUcret / geceSayisi, 2, MidpointRounding.AwayFromZero)
            : rezervasyon.ToplamUcret;

        // Son satırda kuruş yuvarlama farkını dengele
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
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvOrani = kdvOrani,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KaynakSatirId = $"{rezervasyon.Id}_{geceTarihi:yyyyMMdd}"
            });
        }

        // Ek hizmetler — check-out'un kalan bakiye kontrolü bu tutarları da kapsadığı için
        // (bkz. RezervasyonService.GetRezervasyonEkHizmetToplamiAsync), faturaya dahil edilmeleri
        // gerekir; aksi halde tahsil edilen toplam faturanın GenelToplam'ini aşar.
        var ekHizmetler = await _dbContext.RezervasyonEkHizmetler
            .AsNoTracking()
            .Where(x => x.RezervasyonId == rezervasyon.Id)
            .ToListAsync(cancellationToken);

        foreach (var ekHizmet in ekHizmetler)
        {
            satirlar.Add(new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.EkHizmet,
                Aciklama = $"Ek hizmet — {ekHizmet.TarifeAdiSnapshot} ({ekHizmet.HizmetTarihi:dd.MM.yyyy})",
                Miktar = ekHizmet.Miktar > 0 ? ekHizmet.Miktar : 1,
                BirimFiyat = ekHizmet.Miktar > 0
                    ? Math.Round(ekHizmet.ToplamTutar / ekHizmet.Miktar, 2, MidpointRounding.AwayFromZero)
                    : ekHizmet.ToplamTutar,
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvOrani = kdvOrani,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KaynakSatirId = $"{rezervasyon.Id}_ekhizmet_{ekHizmet.Id}"
            });
        }

        // Restoran (OdayaEkle) — RestoranOdemeService.CreateOdayaEkleOdemeAsync ayrı bir tutar
        // tablosuna değil, negatif OdemeTutari ile doğrudan RezervasyonOdemeler'e yazıyor. Bunlar
        // da tahsil edilen toplamın parçası olduğu için faturaya dahil edilmezse fatura tutarı
        // tahsilattan az kalır ve kapama matematiği bozulur.
        var restoranOdemeleri = await _dbContext.RezervasyonOdemeler
            .AsNoTracking()
            .Where(x => x.RezervasyonId == rezervasyon.Id && x.OdemeTipi == OdemeYontemleri.OdayaEkle)
            .ToListAsync(cancellationToken);

        foreach (var restoranOdeme in restoranOdemeleri)
        {
            var tutar = Math.Abs(restoranOdeme.OdemeTutari);
            if (tutar <= 0m)
            {
                continue;
            }

            satirlar.Add(new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.YiyecekIcecek,
                Aciklama = restoranOdeme.Aciklama is { Length: > 0 }
                    ? restoranOdeme.Aciklama
                    : $"Restoran — {restoranOdeme.OdemeTarihi:dd.MM.yyyy}",
                Miktar = 1,
                BirimFiyat = tutar,
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvOrani = kdvOrani,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KaynakSatirId = $"{rezervasyon.Id}_restoran_{restoranOdeme.Id}"
            });
        }

        return satirlar;
    }

    // ──────────────────────────────────────────────
    //  Private — Müşteri bilgilerini çözümleme
    //             Request önceliklidir, boş alanlar rezervasyondan tamamlanır.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Request'teki fatura bilgilerini rezervasyon verisiyle birleştirir.
    /// Request değerleri önceliklidir; boş kalan bireysel alanlar rezervasyondan doldurulur.
    /// Kurumsal fatura validasyonu yapılır.
    /// </summary>
    private static (
        bool kurumsalMi,
        string? musteriUnvan,
        string? musteriAdSoyad,
        string? musteriVergiNo,
        string? musteriTcKimlikNo)
        ResolveMusteriBilgileri(
            RezervasyonSatisBelgesiTaslakRequest request,
            Rezervasyon rezervasyon)
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
            // Ad soyad: request öncelikli, boşsa rezervasyondan
            var adSoyad = !string.IsNullOrWhiteSpace(request.MusteriAdSoyad)
                ? request.MusteriAdSoyad
                : rezervasyon.MisafirAdiSoyadi;

            if (string.IsNullOrWhiteSpace(adSoyad))
            {
                throw new BaseException("Bireysel fatura için müşteri ad soyad zorunludur.", 400);
            }

            // TC kimlik no: request öncelikli, boşsa rezervasyondan
            var tcKimlikNo = !string.IsNullOrWhiteSpace(request.MusteriTcKimlikNo)
                ? request.MusteriTcKimlikNo
                : rezervasyon.TcKimlikNo;

            return (
                kurumsalMi: false,
                musteriUnvan: null,
                musteriAdSoyad: adSoyad,
                musteriVergiNo: null,
                musteriTcKimlikNo: tcKimlikNo
            );
        }
    }
}

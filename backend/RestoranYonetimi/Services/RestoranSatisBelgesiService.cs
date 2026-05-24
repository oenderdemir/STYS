using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.Muhasebe.SatisBelgeleri.Services;
using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranYonetimi.Services;

/// <summary>
/// Restoran sipariş verisinden ortak satış belgesi taslağı oluşturma servisi.
/// Restoran modülü doğrudan SatisBelgesi entity'si oluşturmaz;
/// bunun yerine ISatisBelgesiTaslakOlusturmaService üzerinden fatura altyapısına
/// sipariş verisini iletir.
/// </summary>
public class RestoranSatisBelgesiService : IRestoranSatisBelgesiService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ISatisBelgesiTaslakOlusturmaService _taslakOlusturmaService;
    private readonly ILogger<RestoranSatisBelgesiService> _logger;

    private const string KaynakTipiRestoranSiparis = "RestoranSiparis";
    private const decimal VarsayilanKdvOrani = 10m;

    public RestoranSatisBelgesiService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ISatisBelgesiTaslakOlusturmaService taslakOlusturmaService,
        ILogger<RestoranSatisBelgesiService> logger)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _taslakOlusturmaService = taslakOlusturmaService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SatisBelgesiDto> SatisBelgesiTaslagiOlusturAsync(
        int siparisId,
        RestoranSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Route ID ile body ID eşleşmeli
        if (siparisId != request.SiparisId)
        {
            throw new BaseException("Sipariş ID uyuşmazlığı: route ve body farklı.", 400);
        }

        // 2. Siparişi bul (Kalemler ve Restoran navigation'ları dahil) ve access scope kontrolü yap
        var siparis = await GetScopedSiparisAsync(siparisId, cancellationToken);

        // 3. Durum validasyonu: yalnızca Tamamlandi siparişler için taslak oluşturulabilir
        ValidateSiparisDurumu(siparis);

        // 4. Aktif kalemleri filtrele (iptal edilmemiş olanlar)
        var aktifKalemler = siparis.Kalemler
            .Where(k => k.Durum != RestoranSiparisKalemDurumlari.Iptal)
            .ToList();

        if (aktifKalemler.Count == 0)
        {
            throw new BaseException(
                "Siparişte fatura edilebilir kalem bulunamadı.",
                400);
        }

        // 5. Müşteri bilgilerini çözümle (restoran siparişinde müşteri bilgisi olmadığı için request zorunludur)
        var musteriBilgi = ResolveMusteriBilgileri(request);

        // 6. Toplam tutar validasyonu
        if (siparis.ToplamTutar <= 0)
        {
            throw new BaseException(
                "Sipariş toplam tutarı bulunamadığı için satış belgesi taslağı oluşturulamaz.",
                400);
        }

        // 7. Satış satırlarını oluştur (KDV override bilgileriyle)
        var satirlar = await BuildSatirlarAsync(siparis, aktifKalemler, request, cancellationToken);

        // 8. Belge tarihi ve açıklama (request öncelikli)
        var belgeTarihi = request.BelgeTarihi ?? siparis.SiparisTarihi.Date;
        var aciklama = request.Aciklama
            ?? $"Restoran siparişi: {siparis.SiparisNo}";

        // 9. Taslak request oluştur
        var taslakRequest = new SatisBelgesiTaslakOlusturRequest
        {
            KaynakModul = SatisKaynakModulu.Restoran,
            KaynakTipi = KaynakTipiRestoranSiparis,
            KaynakId = siparisId.ToString(),
            TesisId = siparis.Restoran?.TesisId,
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

        // 10. ISatisBelgesiTaslakOlusturmaService'e ilet
        _logger.LogInformation(
            "Restoran siparişi #{SiparisId} için satış belgesi taslağı oluşturuluyor. Kalem sayısı: {KalemSayisi}, Toplam tutar: {ToplamTutar}, Kurumsal: {KurumsalMi}",
            siparisId, aktifKalemler.Count, siparis.ToplamTutar, musteriBilgi.kurumsalMi);

        var result = await _taslakOlusturmaService.KaynaktanTaslakOlusturAsync(taslakRequest, cancellationToken);

        _logger.LogInformation(
            "Restoran siparişi #{SiparisId} için satış belgesi taslağı oluşturuldu. BelgeId: {BelgeId}, BelgeNo: {BelgeNo}",
            siparisId, result.Id, result.BelgeNo);

        return result;
    }

    // ──────────────────────────────────────────────
    //  Private — Sipariş bulma ve access scope
    // ──────────────────────────────────────────────

    private async Task<RestoranSiparis> GetScopedSiparisAsync(int siparisId, CancellationToken cancellationToken)
    {
        if (siparisId <= 0)
        {
            throw new BaseException("Geçersiz sipariş ID.", 400);
        }

        var siparis = await _dbContext.RestoranSiparisleri
            .Include(x => x.Restoran)
            .Include(x => x.Kalemler)
            .FirstOrDefaultAsync(x => x.Id == siparisId, cancellationToken);

        if (siparis is null)
        {
            throw new BaseException("Sipariş bulunamadı.", 404);
        }

        // Access scope kontrolü: RestoranSiparis → Restoran → TesisId
        if (siparis.Restoran is not null)
        {
            var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
            if (scope.IsScoped && !scope.TesisIds.Contains(siparis.Restoran.TesisId))
            {
                throw new BaseException("Bu sipariş için yetkiniz bulunmuyor.", 403);
            }
        }

        return siparis;
    }

    // ──────────────────────────────────────────────
    //  Private — Durum validasyonu
    // ──────────────────────────────────────────────

    private static void ValidateSiparisDurumu(RestoranSiparis siparis)
    {
        if (siparis.SiparisDurumu == RestoranSiparisDurumlari.Iptal)
        {
            throw new BaseException("İptal edilen sipariş için satış belgesi taslağı oluşturulamaz.", 400);
        }

        if (siparis.SiparisDurumu != RestoranSiparisDurumlari.Tamamlandi)
        {
            throw new BaseException(
                $"Satış belgesi taslağı yalnızca tamamlanmış siparişler için oluşturulabilir. Mevcut durum: {siparis.SiparisDurumu}",
                400);
        }
    }

    // ──────────────────────────────────────────────
    //  Private — Satış satırlarını oluşturma
    // ──────────────────────────────────────────────

    private async Task<List<SatisBelgesiTaslakSatirRequest>> BuildSatirlarAsync(
        RestoranSiparis siparis,
        List<RestoranSiparisKalemi> aktifKalemler,
        RestoranSatisBelgesiTaslakRequest request,
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
            kdvUygulamaTipi = KdvUygulamaTipi.Kdvli;
            kdvOrani = request.KdvOrani ?? VarsayilanKdvOrani;
        }

        // Kuruş yuvarlama dengelemesi için toplam dağıtılanı hesapla
        var satirlar = new List<SatisBelgesiTaslakSatirRequest>(aktifKalemler.Count);
        decimal toplamDagitilan = 0;

        for (var i = 0; i < aktifKalemler.Count; i++)
        {
            var kalem = aktifKalemler[i];
            // Kalemin kendi SatirToplam'ını kullan; son kalemde varsa yuvarlama farkını ekle
            var satirToplam = kalem.SatirToplam;
            toplamDagitilan += satirToplam;

            satirlar.Add(new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = SatisBelgesiSatirTipi.YiyecekIcecek,
                Aciklama = kalem.UrunAdiSnapshot,
                Miktar = kalem.Miktar,
                BirimFiyat = kalem.BirimFiyat,
                KdvUygulamaTipi = kdvUygulamaTipi,
                KdvOrani = kdvOrani,
                KdvIstisnaTanimId = kdvIstisnaTanimId,
                KaynakSatirId = $"{siparis.Id}_{kalem.Id}"
            });
        }

        // Kuruş yuvarlama farkını son satıra ekle
        var fark = siparis.ToplamTutar - toplamDagitilan;
        if (fark != 0 && satirlar.Count > 0)
        {
            var sonSatir = satirlar[^1];
            satirlar[^1] = new SatisBelgesiTaslakSatirRequest
            {
                SatirTipi = sonSatir.SatirTipi,
                Aciklama = sonSatir.Aciklama,
                Miktar = sonSatir.Miktar,
                BirimFiyat = sonSatir.BirimFiyat + fark,
                KdvUygulamaTipi = sonSatir.KdvUygulamaTipi,
                KdvOrani = sonSatir.KdvOrani,
                KdvIstisnaTanimId = sonSatir.KdvIstisnaTanimId,
                KaynakSatirId = sonSatir.KaynakSatirId
            };
        }

        return satirlar;
    }

    // ──────────────────────────────────────────────
    //  Private — Müşteri bilgilerini çözümleme
    //             Restoran siparişinde müşteri bilgisi olmadığı için
    //             request'teki alanlar zorunludur.
    // ──────────────────────────────────────────────

    private static (
        bool kurumsalMi,
        string? musteriUnvan,
        string? musteriAdSoyad,
        string? musteriVergiNo,
        string? musteriTcKimlikNo)
        ResolveMusteriBilgileri(RestoranSatisBelgesiTaslakRequest request)
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
            if (string.IsNullOrWhiteSpace(request.MusteriAdSoyad))
            {
                throw new BaseException("Bireysel fatura için müşteri ad soyad zorunludur.", 400);
            }

            return (
                kurumsalMi: false,
                musteriUnvan: null,
                musteriAdSoyad: request.MusteriAdSoyad,
                musteriVergiNo: null,
                musteriTcKimlikNo: request.MusteriTcKimlikNo
            );
        }
    }
}

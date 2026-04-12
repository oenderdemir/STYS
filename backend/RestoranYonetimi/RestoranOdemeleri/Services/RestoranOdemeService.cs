using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Rezervasyonlar;
using STYS.Rezervasyonlar.Entities;
using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranOdemeleri.Entities;
using STYS.RestoranOdemeleri.Repositories;
using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Entities;
using STYS.RestoranSiparisleri.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranOdemeleri.Services;

public class RestoranOdemeService : IRestoranOdemeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranSiparisRepository _siparisRepository;
    private readonly IRestoranOdemeRepository _odemeRepository;
    private readonly IMapper _mapper;

    public RestoranOdemeService(
        StysAppDbContext dbContext,
        IRestoranSiparisRepository siparisRepository,
        IRestoranOdemeRepository odemeRepository,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _siparisRepository = siparisRepository;
        _odemeRepository = odemeRepository;
        _mapper = mapper;
    }

    public async Task<List<RestoranOdemeDto>> GetBySiparisIdAsync(int siparisId, CancellationToken cancellationToken = default)
    {
        var list = await _odemeRepository.GetBySiparisIdAsync(siparisId, cancellationToken);
        return _mapper.Map<List<RestoranOdemeDto>>(list);
    }

    public async Task<RestoranSiparisOdemeOzetiDto> GetOdemeOzetiAsync(int siparisId, CancellationToken cancellationToken = default)
    {
        var siparis = await GetSiparisForPaymentAsync(siparisId, cancellationToken);
        RecalculateSiparisTotals(siparis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RestoranSiparisOdemeOzetiDto
        {
            SiparisToplami = siparis.ToplamTutar,
            OdenenTutar = siparis.OdenenTutar,
            KalanTutar = siparis.KalanTutar,
            OdemeDurumu = siparis.OdemeDurumu,
            Odemeler = _mapper.Map<List<RestoranOdemeDto>>(siparis.Odemeler.OrderByDescending(x => x.OdemeTarihi).ThenByDescending(x => x.Id).ToList())
        };
    }

    public Task<RestoranOdemeDto> CreateNakitOdemeAsync(int siparisId, CreateNakitOdemeRequest request, CancellationToken cancellationToken = default)
        => CreateRegularPaymentAsync(siparisId, request.Tutar, request.Aciklama, RestoranOdemeTipleri.Nakit, cancellationToken);

    public Task<RestoranOdemeDto> CreateKrediKartiOdemeAsync(int siparisId, CreateKrediKartiOdemeRequest request, CancellationToken cancellationToken = default)
        => CreateRegularPaymentAsync(siparisId, request.Tutar, request.Aciklama, RestoranOdemeTipleri.KrediKarti, cancellationToken);

    public async Task<RestoranOdemeDto> CreateOdayaEkleOdemeAsync(int siparisId, CreateOdayaEkleOdemeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Tutar <= 0)
        {
            throw new BaseException("Odeme tutari sifirdan buyuk olmalidir.", 400);
        }

        var siparis = await GetSiparisForPaymentAsync(siparisId, cancellationToken);
        EnsureSiparisCanBePaid(siparis);

        if (request.Tutar > siparis.KalanTutar)
        {
            throw new BaseException("Tutar siparis kalan tutarini asamaz.", 400);
        }

        if (await _odemeRepository.HasCompletedRoomChargeAsync(siparis.Id, request.RezervasyonId, cancellationToken))
        {
            throw new BaseException("Ayni siparis daha once ayni rezervasyona odaya eklenmistir.", 400);
        }

        var rezervasyon = await _dbContext.Rezervasyonlar.FirstOrDefaultAsync(x => x.Id == request.RezervasyonId, cancellationToken)
            ?? throw new BaseException("Rezervasyon bulunamadi.", 404);

        var restoranTesisId = await _dbContext.Restoranlar
            .Where(x => x.Id == siparis.RestoranId)
            .Select(x => x.TesisId)
            .FirstOrDefaultAsync(cancellationToken);

        if (restoranTesisId <= 0 || restoranTesisId != rezervasyon.TesisId)
        {
            throw new BaseException("Odaya ekleme sadece ayni tesis icindeki rezervasyona yapilabilir.", 400);
        }

        if (rezervasyon.RezervasyonDurumu == RezervasyonDurumlari.Iptal || rezervasyon.RezervasyonDurumu == RezervasyonDurumlari.CheckOutTamamlandi)
        {
            throw new BaseException("Iptal veya check-out olmus rezervasyona odaya ekleme yapilamaz.", 400);
        }

        if (rezervasyon.RezervasyonDurumu != RezervasyonDurumlari.CheckInTamamlandi)
        {
            throw new BaseException("Odaya ekleme icin rezervasyonun check-in asamasinda olmasi gerekir.", 400);
        }

        var restoranAd = await _dbContext.Restoranlar
            .Where(x => x.Id == siparis.RestoranId)
            .Select(x => x.Ad)
            .FirstOrDefaultAsync(cancellationToken) ?? "Restoran";

        var masaNo = string.Empty;
        if (siparis.RestoranMasaId.HasValue)
        {
            masaNo = await _dbContext.RestoranMasalari
                .Where(x => x.Id == siparis.RestoranMasaId.Value)
                .Select(x => x.MasaNo)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        var refNo = $"ROOMCHG-{siparis.SiparisNo}-{request.RezervasyonId}";

        var rezervasyonOdeme = new RezervasyonOdeme
        {
            RezervasyonId = rezervasyon.Id,
            OdemeTarihi = DateTime.UtcNow,
            OdemeTutari = -request.Tutar,
            ParaBirimi = siparis.ParaBirimi,
            OdemeTipi = "OdayaEkle",
            Aciklama = BuildRoomChargeDescription(restoranAd, siparis.SiparisNo, masaNo, request.Aciklama)
        };

        _dbContext.RezervasyonOdemeler.Add(rezervasyonOdeme);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var odeme = new RestoranOdeme
        {
            RestoranSiparisId = siparis.Id,
            OdemeTipi = RestoranOdemeTipleri.OdayaEkle,
            Tutar = request.Tutar,
            ParaBirimi = siparis.ParaBirimi,
            OdemeTarihi = DateTime.UtcNow,
            Aciklama = BuildRoomChargeDescription(restoranAd, siparis.SiparisNo, masaNo, request.Aciklama),
            RezervasyonId = rezervasyon.Id,
            RezervasyonOdemeId = rezervasyonOdeme.Id,
            Durum = RestoranOdemeDurumlari.Tamamlandi,
            IslemReferansNo = refNo
        };

        _dbContext.RestoranOdemeleri.Add(odeme);
        siparis.Odemeler.Add(odeme);

        RecalculateSiparisTotals(siparis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RestoranOdemeDto>(odeme);
    }

    private async Task<RestoranOdemeDto> CreateRegularPaymentAsync(int siparisId, decimal tutar, string? aciklama, string odemeTipi, CancellationToken cancellationToken)
    {
        if (tutar <= 0)
        {
            throw new BaseException("Odeme tutari sifirdan buyuk olmalidir.", 400);
        }

        var siparis = await GetSiparisForPaymentAsync(siparisId, cancellationToken);
        EnsureSiparisCanBePaid(siparis);

        if (tutar > siparis.KalanTutar)
        {
            throw new BaseException("Fazla odeme yapilamaz.", 400);
        }

        var odeme = new RestoranOdeme
        {
            RestoranSiparisId = siparis.Id,
            OdemeTipi = odemeTipi,
            Tutar = tutar,
            ParaBirimi = siparis.ParaBirimi,
            OdemeTarihi = DateTime.UtcNow,
            Aciklama = NormalizeOptional(aciklama, 512),
            Durum = RestoranOdemeDurumlari.Tamamlandi,
            IslemReferansNo = $"RSPPAY-{siparis.SiparisNo}-{Guid.NewGuid():N}"[..64]
        };

        _dbContext.RestoranOdemeleri.Add(odeme);
        siparis.Odemeler.Add(odeme);

        RecalculateSiparisTotals(siparis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<RestoranOdemeDto>(odeme);
    }

    private async Task<RestoranSiparis> GetSiparisForPaymentAsync(int siparisId, CancellationToken cancellationToken)
        => await _siparisRepository.GetDetayByIdAsync(siparisId, cancellationToken)
            ?? throw new BaseException("Siparis bulunamadi.", 404);

    private static void EnsureSiparisCanBePaid(RestoranSiparis siparis)
    {
        if (siparis.SiparisDurumu is RestoranSiparisDurumlari.Iptal)
        {
            throw new BaseException("Iptal siparise odeme eklenemez.", 400);
        }

        if (siparis.SiparisDurumu is RestoranSiparisDurumlari.Tamamlandi)
        {
            throw new BaseException("Tamamlanan sipariste odeme degisikligi yapilamaz.", 400);
        }

        if (siparis.KalanTutar <= 0)
        {
            throw new BaseException("Siparisin kalan tutari yok.", 400);
        }
    }

    private static void RecalculateSiparisTotals(RestoranSiparis siparis)
    {
        var completedPayments = siparis.Odemeler
            .Where(x => x.Durum == RestoranOdemeDurumlari.Tamamlandi)
            .Sum(x => x.Tutar);

        siparis.OdenenTutar = completedPayments;
        siparis.KalanTutar = Math.Max(0m, siparis.ToplamTutar - siparis.OdenenTutar);
        siparis.OdemeDurumu = siparis.OdenenTutar <= 0m
            ? RestoranSiparisOdemeDurumlari.Odenmedi
            : siparis.OdenenTutar < siparis.ToplamTutar
                ? RestoranSiparisOdemeDurumlari.KismiOdendi
                : RestoranSiparisOdemeDurumlari.Odendi;
    }

    private static string BuildRoomChargeDescription(string restoranAd, string siparisNo, string? masaNo, string? aciklama)
    {
        var baseText = $"Restoran: {restoranAd}, Siparis: {siparisNo}";
        if (!string.IsNullOrWhiteSpace(masaNo))
        {
            baseText += $", Masa: {masaNo}";
        }

        if (!string.IsNullOrWhiteSpace(aciklama))
        {
            baseText += $" | {aciklama.Trim()}";
        }

        return baseText.Length > 512 ? baseText[..512] : baseText;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}

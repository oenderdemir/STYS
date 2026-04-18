using Microsoft.EntityFrameworkCore;
using STYS.GarsonServis.Dtos;
using STYS.Infrastructure.EntityFramework;
using STYS.Licensing;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMenuUrunleri.Entities;
using STYS.RestoranOdemeleri.Entities;
using STYS.RestoranSiparisleri.Entities;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.GarsonServis.Services;

public class GarsonServisService : IGarsonServisService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranErisimService _restoranErisimService;
    private readonly ILicenseService _licenseService;

    public GarsonServisService(
        StysAppDbContext dbContext,
        IRestoranErisimService restoranErisimService,
        ILicenseService licenseService)
    {
        _dbContext = dbContext;
        _restoranErisimService = restoranErisimService;
        _licenseService = licenseService;
    }

    public async Task<List<GarsonMasaDto>> GetMasalarAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);
        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);

        var masalar = await _dbContext.RestoranMasalari
            .Where(x => x.RestoranId == restoranId && x.AktifMi)
            .OrderBy(x => x.MasaNo)
            .ToListAsync(cancellationToken);

        var acikSiparisler = await _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Where(x => x.RestoranId == restoranId
                && x.RestoranMasaId.HasValue
                && RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(x.SiparisDurumu))
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var acikSiparisByMasa = acikSiparisler
            .GroupBy(x => x.RestoranMasaId!.Value)
            .ToDictionary(x => x.Key, x => x.First());

        return masalar.Select(masa =>
        {
            acikSiparisByMasa.TryGetValue(masa.Id, out var acikSiparis);
            return new GarsonMasaDto
            {
                MasaId = masa.Id,
                MasaNo = masa.MasaNo,
                Durum = ResolveGarsonMasaDurumu(masa, acikSiparis),
                AktifOturumId = acikSiparis?.Id,
                AktifOturumToplamTutar = acikSiparis?.ToplamTutar,
                AktifKalemSayisi = acikSiparis?.Kalemler.Count ?? 0,
                SonIslemZamani = acikSiparis?.UpdatedAt ?? acikSiparis?.CreatedAt
            };
        }).ToList();
    }

    public async Task<MasaOturumuDto?> GetMasaOturumuByMasaAsync(int masaId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        var masaRestoranId = await _dbContext.RestoranMasalari
            .Where(x => x.Id == masaId)
            .Select(x => x.RestoranId)
            .FirstOrDefaultAsync(cancellationToken);

        if (masaRestoranId > 0)
        {
            await _restoranErisimService.EnsureRestoranErisimiAsync(masaRestoranId, cancellationToken);
        }

        var entity = await _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Include(x => x.RestoranMasa)
            .FirstOrDefaultAsync(
                x => x.RestoranMasaId == masaId && RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(x.SiparisDurumu),
                cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<MasaOturumuDto> StartOrGetMasaOturumuAsync(int masaId, CreateMasaOturumuRequest request, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        var masa = await _dbContext.RestoranMasalari
            .Include(x => x.Restoran)
            .FirstOrDefaultAsync(x => x.Id == masaId, cancellationToken)
            ?? throw new BaseException("Masa bulunamadi.", 404);

        await _restoranErisimService.EnsureRestoranErisimiAsync(masa.RestoranId, cancellationToken);

        if (!masa.AktifMi)
        {
            throw new BaseException("Pasif masa icin oturum acilamaz.", 400);
        }

        if (masa.Durum == RestoranMasaDurumlari.Kapali)
        {
            throw new BaseException("Kapali masa icin oturum acilamaz.", 400);
        }

        if (masa.Restoran is null || !masa.Restoran.AktifMi)
        {
            throw new BaseException("Pasif restoran icin oturum acilamaz.", 400);
        }

        var existing = await _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Include(x => x.RestoranMasa)
            .FirstOrDefaultAsync(
                x => x.RestoranMasaId == masaId && RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(x.SiparisDurumu),
                cancellationToken);

        if (existing is not null)
        {
            return ToDto(existing);
        }

        var entity = new RestoranSiparis
        {
            RestoranId = masa.RestoranId,
            RestoranMasaId = masa.Id,
            SiparisNo = await GenerateSiparisNoAsync(cancellationToken),
            SiparisDurumu = RestoranSiparisDurumlari.Taslak,
            ParaBirimi = NormalizeCurrency(request.ParaBirimi),
            Notlar = null,
            SiparisTarihi = DateTime.UtcNow,
            ToplamTutar = 0,
            OdenenTutar = 0,
            KalanTutar = 0,
            OdemeDurumu = RestoranSiparisOdemeDurumlari.Odenmedi
        };

        masa.Durum = RestoranMasaDurumlari.Dolu;
        _dbContext.RestoranSiparisleri.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        entity.RestoranMasa = masa;
        return ToDto(entity);
    }

    public async Task<MasaOturumuDto> AddKalemAsync(int oturumId, AddMasaOturumuKalemiRequest request, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        if (request.UrunId <= 0)
        {
            throw new BaseException("Urun secimi zorunludur.", 400);
        }

        if (request.Miktar <= 0)
        {
            throw new BaseException("Miktar sifirdan buyuk olmalidir.", 400);
        }

        var oturum = await GetMutableOturumAsync(oturumId, cancellationToken);

        var urun = await _dbContext.RestoranMenuUrunleri
            .Include(x => x.RestoranMenuKategori)
            .ThenInclude(x => x!.Restoran)
            .FirstOrDefaultAsync(x => x.Id == request.UrunId, cancellationToken)
            ?? throw new BaseException("Urun bulunamadi.", 404);

        ValidateUrunForOturum(oturum, urun);

        var normalizedKalemNotu = NormalizeOptional(request.Notlar, 512);
        var existingKalem = oturum.Kalemler.FirstOrDefault(x =>
            x.RestoranMenuUrunId == urun.Id
            && x.Durum != RestoranSiparisKalemDurumlari.ServisEdildi
            && x.Durum != RestoranSiparisKalemDurumlari.Iptal
            && string.Equals(x.Notlar ?? string.Empty, normalizedKalemNotu ?? string.Empty, StringComparison.Ordinal));

        if (existingKalem is not null)
        {
            existingKalem.Miktar = Math.Round(existingKalem.Miktar + request.Miktar, 2, MidpointRounding.AwayFromZero);
            existingKalem.SatirToplam = Math.Round(existingKalem.Miktar * existingKalem.BirimFiyat, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            var kalem = new RestoranSiparisKalemi
            {
                RestoranSiparisId = oturum.Id,
                RestoranMenuUrunId = urun.Id,
                UrunAdiSnapshot = urun.Ad,
                BirimFiyat = urun.Fiyat,
                Miktar = Math.Round(request.Miktar, 2, MidpointRounding.AwayFromZero),
                SatirToplam = Math.Round(request.Miktar * urun.Fiyat, 2, MidpointRounding.AwayFromZero),
                Durum = RestoranSiparisKalemDurumlari.Beklemede,
                Notlar = normalizedKalemNotu
            };

            oturum.Kalemler.Add(kalem);
        }

        RecalculateSiparisTotals(oturum);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(oturum);
    }

    public async Task<MasaOturumuDto> UpdateKalemAsync(int oturumId, int kalemId, UpdateMasaOturumuKalemiRequest request, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        var oturum = await GetMutableOturumAsync(oturumId, cancellationToken);
        var kalem = oturum.Kalemler.FirstOrDefault(x => x.Id == kalemId)
            ?? throw new BaseException("Kalem bulunamadi.", 404);

        if (request.Miktar <= 0)
        {
            _dbContext.RestoranSiparisKalemleri.Remove(kalem);
            oturum.Kalemler.Remove(kalem);
        }
        else
        {
            kalem.Miktar = Math.Round(request.Miktar, 2, MidpointRounding.AwayFromZero);
            kalem.Durum = NormalizeKalemDurumu(request.Durum, kalem.Durum);
            kalem.Notlar = NormalizeOptional(request.Notlar, 512);
            kalem.SatirToplam = Math.Round(kalem.Miktar * kalem.BirimFiyat, 2, MidpointRounding.AwayFromZero);
        }

        RecalculateSiparisTotals(oturum);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(oturum);
    }

    public async Task<MasaOturumuDto> DeleteKalemAsync(int oturumId, int kalemId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        var oturum = await GetMutableOturumAsync(oturumId, cancellationToken);
        var kalem = oturum.Kalemler.FirstOrDefault(x => x.Id == kalemId)
            ?? throw new BaseException("Kalem bulunamadi.", 404);

        _dbContext.RestoranSiparisKalemleri.Remove(kalem);
        oturum.Kalemler.Remove(kalem);
        RecalculateSiparisTotals(oturum);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(oturum);
    }

    public async Task<MasaOturumuDto> UpdateNotAsync(int oturumId, UpdateMasaOturumuNotRequest request, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        var oturum = await GetMutableOturumAsync(oturumId, cancellationToken);
        oturum.Notlar = NormalizeOptional(request.Notlar, 1024);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(oturum);
    }

    public async Task<MasaOturumuDto> UpdateDurumAsync(int oturumId, UpdateMasaOturumuDurumRequest request, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Durum))
        {
            throw new BaseException("Durum zorunludur.", 400);
        }

        var oturum = await _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Include(x => x.RestoranMasa)
            .FirstOrDefaultAsync(x => x.Id == oturumId, cancellationToken)
            ?? throw new BaseException("Masa oturumu bulunamadi.", 404);

        EnsureOpenOturum(oturum);

        var targetSiparisDurumu = ResolveSiparisDurumu(request.Durum);
        if (targetSiparisDurumu == RestoranSiparisDurumlari.Iptal
            && oturum.Odemeler.Any(x => x.Durum == RestoranOdemeDurumlari.Tamamlandi))
        {
            throw new BaseException("Tamamlanmis odemesi olan oturum iptal edilemez.", 400);
        }

        oturum.SiparisDurumu = targetSiparisDurumu;
        if (oturum.RestoranMasa is not null)
        {
            SetMasaDurumuBySiparis(oturum.RestoranMasa, targetSiparisDurumu);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(oturum);
    }

    public async Task<GarsonMenuDto> GetMenuAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Restoran, cancellationToken);
        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);

        var restoranAktifMi = await _dbContext.Restoranlar
            .Where(x => x.Id == restoranId)
            .Select(x => x.AktifMi)
            .FirstOrDefaultAsync(cancellationToken);

        if (!restoranAktifMi)
        {
            throw new BaseException("Restoran aktif degil veya bulunamadi.", 404);
        }

        var kategoriler = await _dbContext.RestoranMenuKategorileri
            .Where(x => x.RestoranId == restoranId && x.AktifMi)
            .OrderBy(x => x.SiraNo)
            .ThenBy(x => x.Ad)
            .Select(x => new
            {
                x.Id,
                x.Ad,
                x.SiraNo
            })
            .ToListAsync(cancellationToken);

        var kategoriIds = kategoriler.Select(x => x.Id).ToList();
        var urunler = await _dbContext.RestoranMenuUrunleri
            .Where(x => kategoriIds.Contains(x.RestoranMenuKategoriId) && x.AktifMi)
            .OrderBy(x => x.Ad)
            .ToListAsync(cancellationToken);

        return new GarsonMenuDto
        {
            RestoranId = restoranId,
            Kategoriler = kategoriler.Select(kategori => new GarsonMenuKategoriDto
            {
                Id = kategori.Id,
                Ad = kategori.Ad,
                SiraNo = kategori.SiraNo,
                Urunler = urunler
                    .Where(x => x.RestoranMenuKategoriId == kategori.Id)
                    .Select(urun => new GarsonMenuUrunDto
                    {
                        Id = urun.Id,
                        KategoriId = urun.RestoranMenuKategoriId,
                        Ad = urun.Ad,
                        Aciklama = urun.Aciklama,
                        Fiyat = urun.Fiyat,
                        ParaBirimi = urun.ParaBirimi,
                        HazirlamaSuresiDakika = urun.HazirlamaSuresiDakika
                    })
                    .ToList()
            }).ToList()
        };
    }

    private async Task<RestoranSiparis> GetMutableOturumAsync(int oturumId, CancellationToken cancellationToken)
    {
        var oturum = await _dbContext.RestoranSiparisleri
            .Include(x => x.Kalemler)
            .Include(x => x.Odemeler)
            .Include(x => x.RestoranMasa)
            .FirstOrDefaultAsync(x => x.Id == oturumId, cancellationToken)
            ?? throw new BaseException("Masa oturumu bulunamadi.", 404);

        await _restoranErisimService.EnsureRestoranErisimiAsync(oturum.RestoranId, cancellationToken);

        EnsureOpenOturum(oturum);
        return oturum;
    }

    private static void EnsureOpenOturum(RestoranSiparis oturum)
    {
        if (!RestoranSiparisDurumlari.AcikSiparisDurumlari.Contains(oturum.SiparisDurumu))
        {
            throw new BaseException("Kapali oturumda degisiklik yapilamaz.", 400);
        }
    }

    private void ValidateUrunForOturum(RestoranSiparis oturum, RestoranMenuUrun urun)
    {
        if (!urun.AktifMi)
        {
            throw new BaseException("Pasif urun eklenemez.", 400);
        }

        if (urun.RestoranMenuKategori is null || !urun.RestoranMenuKategori.AktifMi)
        {
            throw new BaseException("Pasif kategori urunu eklenemez.", 400);
        }

        if (urun.RestoranMenuKategori.Restoran is null || !urun.RestoranMenuKategori.Restoran.AktifMi)
        {
            throw new BaseException("Pasif restorana ait urun eklenemez.", 400);
        }

        if (urun.RestoranMenuKategori.RestoranId != oturum.RestoranId)
        {
            throw new BaseException("Urun secili masa oturumu restorani ile uyusmuyor.", 400);
        }

        if (!string.Equals(urun.ParaBirimi, oturum.ParaBirimi, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Urun para birimi masa oturumu ile uyusmuyor.", 400);
        }

        if (oturum.RestoranMasa is not null)
        {
            if (!oturum.RestoranMasa.AktifMi || oturum.RestoranMasa.Durum == RestoranMasaDurumlari.Kapali)
            {
                throw new BaseException("Pasif/kapali masa icin urun eklenemez.", 400);
            }
        }
    }

    private static void RecalculateSiparisTotals(RestoranSiparis siparis)
    {
        siparis.ToplamTutar = siparis.Kalemler.Sum(x => x.SatirToplam);
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

    private static string ResolveSiparisDurumu(string requestDurum)
    {
        var normalized = requestDurum.Trim().ToLowerInvariant();
        return normalized switch
        {
            "taslak" => RestoranSiparisDurumlari.Taslak,
            "hazirlaniyor" => RestoranSiparisDurumlari.Hazirlaniyor,
            "hazir" => RestoranSiparisDurumlari.Hazir,
            "serviste" => RestoranSiparisDurumlari.Serviste,
            "hesapistendi" => RestoranSiparisDurumlari.Hazir,
            "tamamlandi" => RestoranSiparisDurumlari.Tamamlandi,
            "kapatildi" => RestoranSiparisDurumlari.Tamamlandi,
            "iptal" => RestoranSiparisDurumlari.Iptal,
            _ => throw new BaseException("Gecersiz oturum durumu.", 400)
        };
    }

    private static string ResolveGarsonMasaDurumu(RestoranMasa masa, RestoranSiparis? acikSiparis)
    {
        if (masa.Durum == RestoranMasaDurumlari.Kapali)
        {
            return RestoranMasaDurumlari.Kapali;
        }

        if (acikSiparis is null)
        {
            return masa.Durum;
        }

        return acikSiparis.SiparisDurumu switch
        {
            RestoranSiparisDurumlari.Hazir => "HesapIstendi",
            RestoranSiparisDurumlari.Serviste => RestoranMasaDurumlari.Serviste,
            _ => RestoranMasaDurumlari.Dolu
        };
    }

    private static void SetMasaDurumuBySiparis(RestoranMasa masa, string siparisDurumu)
    {
        if (siparisDurumu is RestoranSiparisDurumlari.Tamamlandi or RestoranSiparisDurumlari.Iptal)
        {
            masa.Durum = RestoranMasaDurumlari.Musait;
            return;
        }

        masa.Durum = siparisDurumu == RestoranSiparisDurumlari.Serviste
            ? RestoranMasaDurumlari.Serviste
            : RestoranMasaDurumlari.Dolu;
    }

    private static MasaOturumuDto ToDto(RestoranSiparis entity)
    {
        return new MasaOturumuDto
        {
            OturumId = entity.Id,
            RestoranId = entity.RestoranId,
            MasaId = entity.RestoranMasaId ?? 0,
            MasaNo = entity.RestoranMasa?.MasaNo ?? string.Empty,
            Durum = entity.SiparisDurumu == RestoranSiparisDurumlari.Hazir ? "HesapIstendi" : entity.SiparisDurumu,
            Notlar = entity.Notlar,
            ParaBirimi = entity.ParaBirimi,
            ToplamTutar = entity.ToplamTutar,
            SiparisTarihi = entity.SiparisTarihi,
            Kalemler = entity.Kalemler
                .OrderBy(x => x.Id)
                .Select(x => new MasaOturumuKalemiDto
                {
                    Id = x.Id,
                    UrunId = x.RestoranMenuUrunId,
                    UrunAdi = x.UrunAdiSnapshot,
                    BirimFiyat = x.BirimFiyat,
                    Miktar = x.Miktar,
                    SatirToplam = x.SatirToplam,
                    Durum = string.IsNullOrWhiteSpace(x.Durum) ? RestoranSiparisKalemDurumlari.Beklemede : x.Durum,
                    Notlar = x.Notlar
                })
                .ToList()
        };
    }

    private static string NormalizeKalemDurumu(string? requestedDurum, string currentDurum)
    {
        if (string.IsNullOrWhiteSpace(requestedDurum))
        {
            return currentDurum;
        }

        var normalized = requestedDurum.Trim();
        return RestoranSiparisKalemDurumlari.TumDurumlar.Contains(normalized)
            ? normalized
            : throw new BaseException("Gecersiz kalem durumu.", 400);
    }

    private async Task<string> GenerateSiparisNoAsync(CancellationToken cancellationToken)
    {
        var datePrefix = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"RSP-{datePrefix}-";

        var last = await _dbContext.RestoranSiparisleri
            .Where(x => x.SiparisNo.StartsWith(prefix))
            .OrderByDescending(x => x.Id)
            .Select(x => x.SiparisNo)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var parts = last.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && int.TryParse(parts[2], out var current))
            {
                next = current + 1;
            }
        }

        return $"{prefix}{next:D4}";
    }

    private static string NormalizeCurrency(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
        {
            return "TRY";
        }

        return normalized;
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

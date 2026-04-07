using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampPuanKuraliYonetimService : IKampPuanKuraliYonetimService
{
    private readonly StysAppDbContext _dbContext;

    public KampPuanKuraliYonetimService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KampPuanKuraliYonetimBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        var programlar = await _dbContext.KampProgramlari
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Ad)
            .Select(x => new KampProgramiSecenekDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        var globalBasvuruSahibiTipleri = await _dbContext.KampBasvuruSahibiTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Kod)
            .Select(x => new KampPuanBasvuruSahibiTipSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        var tipKurallari = await _dbContext.KampProgramiBasvuruSahibiTipKurallari
            .AsNoTracking()
            .Include(x => x.KampBasvuruSahibiTipi)
            .Include(x => x.KampProgrami)
            .Where(x => x.KampProgrami != null && x.KampProgrami.AktifMi && x.KampBasvuruSahibiTipi != null && x.KampBasvuruSahibiTipi.AktifMi)
            .OrderBy(x => x.KampProgrami!.Ad)
            .ThenBy(x => x.OncelikSirasi)
            .ThenBy(x => x.KampBasvuruSahibiTipi!.Ad)
            .Select(x => new KampPuanBasvuruSahibiTipiDto
            {
                Id = x.Id,
                KampProgramiId = x.KampProgramiId,
                KampBasvuruSahibiTipiId = x.KampBasvuruSahibiTipiId,
                Kod = x.KampBasvuruSahibiTipi != null ? x.KampBasvuruSahibiTipi.Kod : string.Empty,
                Ad = x.KampBasvuruSahibiTipi != null ? x.KampBasvuruSahibiTipi.Ad : string.Empty,
                OncelikSirasi = x.OncelikSirasi,
                TabanPuan = x.TabanPuan,
                HizmetYiliPuaniAktifMi = x.HizmetYiliPuaniAktifMi,
                EmekliBonusPuani = x.EmekliBonusPuani,
                VarsayilanKatilimciTipiKodu = x.VarsayilanKatilimciTipiKodu,
                AktifMi = x.AktifMi
            })
            .ToListAsync(cancellationToken);

        var kuralSetleri = await _dbContext.KampKuralSetleri
            .AsNoTracking()
            .Where(x => x.KampProgrami != null && x.KampProgrami.AktifMi)
            .Include(x => x.KampProgrami)
            .OrderBy(x => x.KampProgrami!.Ad)
            .ThenByDescending(x => x.KampYili)
            .ThenByDescending(x => x.Id)
            .Select(x => new KampPuanKuralSetiDto
            {
                Id = x.Id,
                KampProgramiId = x.KampProgramiId,
                KampProgramiAd = x.KampProgrami != null ? x.KampProgrami.Ad : null,
                KampYili = x.KampYili,
                OncekiYilSayisi = x.OncekiYilSayisi,
                KatilimCezaPuani = x.KatilimCezaPuani,
                KatilimciBasinaPuan = x.KatilimciBasinaPuan,
                AktifMi = x.AktifMi
            })
            .ToListAsync(cancellationToken);

        var katilimciTipleri = await _dbContext.KampKatilimciTipleri
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderBy(x => x.Id)
            .Select(x => new KampSecenekDto
            {
                Kod = x.Kod,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        var yasUcretKurali = await _dbContext.KampYasUcretKurallari
            .AsNoTracking()
            .Where(x => x.AktifMi)
            .OrderByDescending(x => x.Id)
            .Select(x => new KampYasUcretKuraliDto
            {
                Id = x.Id,
                UcretsizCocukMaxYas = x.UcretsizCocukMaxYas,
                YarimUcretliCocukMaxYas = x.YarimUcretliCocukMaxYas,
                YemekOrani = x.YemekOrani,
                AktifMi = x.AktifMi
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? new KampYasUcretKuraliDto();

        return new KampPuanKuraliYonetimBaglamDto
        {
            Programlar = programlar,
            GlobalBasvuruSahibiTipleri = globalBasvuruSahibiTipleri,
            KuralSetleri = kuralSetleri,
            BasvuruSahibiTipleri = tipKurallari,
            KatilimciTipleri = katilimciTipleri,
            YasUcretKurali = yasUcretKurali
        };
    }

    public async Task<KampPuanKuraliYonetimBaglamDto> KaydetAsync(KampPuanKuraliYonetimKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await UpsertKuralSetleriAsync(request.KuralSetleri, cancellationToken);
        await UpsertBasvuruSahibiTipKurallariAsync(request.BasvuruSahibiTipleri, cancellationToken);
        await UpsertYasUcretKuraliAsync(request.YasUcretKurali, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetBaglamAsync(cancellationToken);
    }

    private async Task UpsertKuralSetleriAsync(List<KampPuanKuralSetiDto> dtos, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.KampKuralSetleri.ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(x => x.Id, x => x);

        foreach (var dto in dtos)
        {
            if (dto.Id.HasValue && existingById.TryGetValue(dto.Id.Value, out var entity))
            {
                entity.KampYili = dto.KampYili;
                entity.KampProgramiId = dto.KampProgramiId;
                entity.OncekiYilSayisi = dto.OncekiYilSayisi;
                entity.KatilimCezaPuani = dto.KatilimCezaPuani;
                entity.KatilimciBasinaPuan = dto.KatilimciBasinaPuan;
                entity.AktifMi = dto.AktifMi;
                entity.IsDeleted = false;
                continue;
            }

            _dbContext.KampKuralSetleri.Add(new KampKuralSeti
            {
                KampProgramiId = dto.KampProgramiId,
                KampYili = dto.KampYili,
                OncekiYilSayisi = dto.OncekiYilSayisi,
                KatilimCezaPuani = dto.KatilimCezaPuani,
                KatilimciBasinaPuan = dto.KatilimciBasinaPuan,
                AktifMi = dto.AktifMi
            });
        }
    }

    private async Task UpsertBasvuruSahibiTipKurallariAsync(List<KampPuanBasvuruSahibiTipiDto> dtos, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.KampProgramiBasvuruSahibiTipKurallari.ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(x => x.Id, x => x);

        foreach (var dto in dtos)
        {
            var normalizedVarsayilan = string.IsNullOrWhiteSpace(dto.VarsayilanKatilimciTipiKodu)
                ? null
                : dto.VarsayilanKatilimciTipiKodu.Trim();

            if (dto.Id.HasValue && existingById.TryGetValue(dto.Id.Value, out var entity))
            {
                entity.KampProgramiId = dto.KampProgramiId;
                entity.KampBasvuruSahibiTipiId = dto.KampBasvuruSahibiTipiId;
                entity.OncelikSirasi = dto.OncelikSirasi;
                entity.TabanPuan = dto.TabanPuan;
                entity.HizmetYiliPuaniAktifMi = dto.HizmetYiliPuaniAktifMi;
                entity.EmekliBonusPuani = dto.EmekliBonusPuani;
                entity.VarsayilanKatilimciTipiKodu = normalizedVarsayilan;
                entity.AktifMi = dto.AktifMi;
                entity.IsDeleted = false;
                continue;
            }

            _dbContext.KampProgramiBasvuruSahibiTipKurallari.Add(new KampProgramiBasvuruSahibiTipKurali
            {
                KampProgramiId = dto.KampProgramiId,
                KampBasvuruSahibiTipiId = dto.KampBasvuruSahibiTipiId,
                OncelikSirasi = dto.OncelikSirasi,
                TabanPuan = dto.TabanPuan,
                HizmetYiliPuaniAktifMi = dto.HizmetYiliPuaniAktifMi,
                EmekliBonusPuani = dto.EmekliBonusPuani,
                VarsayilanKatilimciTipiKodu = normalizedVarsayilan,
                AktifMi = dto.AktifMi
            });
        }
    }

    private async Task UpsertYasUcretKuraliAsync(KampYasUcretKuraliDto dto, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.KampYasUcretKurallari
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            _dbContext.KampYasUcretKurallari.Add(new KampYasUcretKurali
            {
                UcretsizCocukMaxYas = dto.UcretsizCocukMaxYas,
                YarimUcretliCocukMaxYas = dto.YarimUcretliCocukMaxYas,
                YemekOrani = dto.YemekOrani,
                AktifMi = dto.AktifMi
            });

            return;
        }

        existing.UcretsizCocukMaxYas = dto.UcretsizCocukMaxYas;
        existing.YarimUcretliCocukMaxYas = dto.YarimUcretliCocukMaxYas;
        existing.YemekOrani = dto.YemekOrani;
        existing.AktifMi = dto.AktifMi;
        existing.IsDeleted = false;
    }

    private static void ValidateRequest(KampPuanKuraliYonetimKaydetRequestDto request)
    {
        if (request.KuralSetleri.Count == 0)
        {
            throw new BaseException("En az bir kamp kural seti tanimi zorunludur.", 400);
        }

        if (request.BasvuruSahibiTipleri.Count == 0)
        {
            throw new BaseException("En az bir basvuru sahibi tipi tanimi zorunludur.", 400);
        }

        var duplicateYears = request.KuralSetleri
            .GroupBy(x => new { x.KampProgramiId, x.KampYili })
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateYears is not null)
        {
            throw new BaseException($"Program {duplicateYears.Key.KampProgramiId} ve {duplicateYears.Key.KampYili} yili icin birden fazla kural seti kaydi gonderildi.", 400);
        }

        var duplicateKod = request.BasvuruSahibiTipleri
            .GroupBy(x => new { x.KampProgramiId, x.KampBasvuruSahibiTipiId })
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateKod is not null)
        {
            throw new BaseException($"Program {duplicateKod.Key.KampProgramiId} icin basvuru sahibi tipi tekrari var.", 400);
        }

        foreach (var kuralSeti in request.KuralSetleri)
        {
            if (kuralSeti.KampProgramiId <= 0)
            {
                throw new BaseException("Kural seti icin kamp programi secimi zorunludur.", 400);
            }

            if (kuralSeti.KampYili < KampValidasyonKurallari.YilRange.Min || kuralSeti.KampYili > KampValidasyonKurallari.YilRange.Max)
            {
                throw new BaseException("Kamp yili 2000-2100 araliginda olmalidir.", 400);
            }

            if (kuralSeti.OncekiYilSayisi < KampValidasyonKurallari.OncekiYilSayisi.Min || kuralSeti.OncekiYilSayisi > KampValidasyonKurallari.OncekiYilSayisi.Max)
            {
                throw new BaseException("Onceki yil sayisi 0-10 araliginda olmalidir.", 400);
            }

            if (kuralSeti.KatilimCezaPuani < KampValidasyonKurallari.KatilimCezaPuani.Min || kuralSeti.KatilimCezaPuani > KampValidasyonKurallari.KatilimCezaPuani.Max)
            {
                throw new BaseException("Katilim ceza puani 0-1000 araliginda olmalidir.", 400);
            }

            if (kuralSeti.KatilimciBasinaPuan < KampValidasyonKurallari.KatilimciBasinaPuan.Min || kuralSeti.KatilimciBasinaPuan > KampValidasyonKurallari.KatilimciBasinaPuan.Max)
            {
                throw new BaseException("Katilimci basina puan 0-1000 araliginda olmalidir.", 400);
            }
        }

        foreach (var tip in request.BasvuruSahibiTipleri)
        {
            if (tip.KampProgramiId <= 0)
            {
                throw new BaseException("Basvuru sahibi tipi kurali icin kamp programi secimi zorunludur.", 400);
            }

            if (tip.KampBasvuruSahibiTipiId <= 0)
            {
                throw new BaseException("Basvuru sahibi tipi kurali icin global tip secimi zorunludur.", 400);
            }

            if (tip.OncelikSirasi < KampValidasyonKurallari.OncelikSirasi.Min || tip.OncelikSirasi > KampValidasyonKurallari.OncelikSirasi.Max)
            {
                throw new BaseException("Oncelik sirasi 0-999 araliginda olmalidir.", 400);
            }

            if (tip.TabanPuan < KampValidasyonKurallari.TabanPuan.Min || tip.TabanPuan > KampValidasyonKurallari.TabanPuan.Max)
            {
                throw new BaseException("Taban puan 0-5000 araliginda olmalidir.", 400);
            }

            if (tip.EmekliBonusPuani < KampValidasyonKurallari.EmekliBonusPuani.Min || tip.EmekliBonusPuani > KampValidasyonKurallari.EmekliBonusPuani.Max)
            {
                throw new BaseException("Emekli bonus puani 0-1000 araliginda olmalidir.", 400);
            }
        }

        if (request.YasUcretKurali.UcretsizCocukMaxYas < KampValidasyonKurallari.UcretsizCocukMaxYas.Min
            || request.YasUcretKurali.UcretsizCocukMaxYas > KampValidasyonKurallari.UcretsizCocukMaxYas.Max)
        {
            throw new BaseException("Ucretsiz cocuk max yas 0-18 araliginda olmalidir.", 400);
        }

        if (request.YasUcretKurali.YarimUcretliCocukMaxYas < KampValidasyonKurallari.YarimUcretliCocukMaxYas.Min
            || request.YasUcretKurali.YarimUcretliCocukMaxYas > KampValidasyonKurallari.YarimUcretliCocukMaxYas.Max)
        {
            throw new BaseException("Yarim ucretli cocuk max yas 0-18 araliginda olmalidir.", 400);
        }

        if (request.YasUcretKurali.YarimUcretliCocukMaxYas < request.YasUcretKurali.UcretsizCocukMaxYas)
        {
            throw new BaseException("Yarim ucretli cocuk max yas, ucretsiz cocuk max yastan kucuk olamaz.", 400);
        }

        if (request.YasUcretKurali.YemekOrani < KampValidasyonKurallari.YemekOrani.Min
            || request.YasUcretKurali.YemekOrani > KampValidasyonKurallari.YemekOrani.Max)
        {
            throw new BaseException("Yemek orani 0.00 - 1.00 araliginda olmalidir.", 400);
        }
    }
}

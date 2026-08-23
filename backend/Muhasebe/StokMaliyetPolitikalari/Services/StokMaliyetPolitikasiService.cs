using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class StokMaliyetPolitikasiService : IStokMaliyetPolitikasiService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public StokMaliyetPolitikasiService(
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService,
        IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
        _tesisScopeService = tesisScopeService;
    }

    public async Task<CurrentStokMaliyetPolitikasiDto> GetCurrentAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
    {
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        var maliYil = await ResolveMaliYilAsync(tesisId, tarih, cancellationToken);
        var politika = await _dbContext.StokMaliyetPolitikalari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TesisId == tesisId && x.MaliYil == maliYil, cancellationToken);

        return new CurrentStokMaliyetPolitikasiDto
        {
            TesisId = tesisId,
            MaliYil = maliYil,
            MaliyetYontemi = politika?.MaliyetYontemi,
            PolitikaSecildiMi = politika is not null
        };
    }

    public async Task<StokMaliyetPolitikasiDto?> GetByTesisMaliYilAsync(int tesisId, int maliYil, CancellationToken cancellationToken = default)
    {
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        var politika = await _dbContext.StokMaliyetPolitikalari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TesisId == tesisId && x.MaliYil == maliYil, cancellationToken);

        return politika is null ? null : Map(politika);
    }

    public async Task<StokMaliyetPolitikasiDto> UpsertAsync(UpsertStokMaliyetPolitikasiRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await _tesisScopeService.EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);

        if (!string.Equals(request.MaliyetYontemi, StokMaliyetYontemleri.FIFO, StringComparison.Ordinal)
            && await HasOpenFifoLayersAsync(request.TesisId, cancellationToken))
        {
            throw new BaseException("Devreden FIFO maliyet katmanları bulunduğu için stok maliyet yöntemi değiştirilemez.", 400);
        }

        var politika = await _dbContext.StokMaliyetPolitikalari
            .FirstOrDefaultAsync(x => x.TesisId == request.TesisId && x.MaliYil == request.MaliYil, cancellationToken);

        if (politika is null)
        {
            politika = new StokMaliyetPolitikasi
            {
                TesisId = request.TesisId,
                MaliYil = request.MaliYil,
                MaliyetYontemi = request.MaliyetYontemi
            };

            _dbContext.StokMaliyetPolitikalari.Add(politika);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Map(politika);
        }

        if (string.Equals(politika.MaliyetYontemi, request.MaliyetYontemi, StringComparison.Ordinal))
        {
            return Map(politika);
        }

        var maliyetlendirilmisHareketVar = await HasCostSnapshottedMovementAsync(request.TesisId, request.MaliYil, cancellationToken);
        if (maliyetlendirilmisHareketVar)
        {
            throw new BaseException("Bu mali yılda maliyetlendirilmiş stok hareketleri bulunduğu için maliyet yöntemi değiştirilemez.", 400);
        }

        politika.MaliyetYontemi = request.MaliyetYontemi;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(politika);
    }

    public async Task<string> GetRequiredMaliyetYontemiAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(tesisId, tarih, cancellationToken);
        if (!current.PolitikaSecildiMi || string.IsNullOrWhiteSpace(current.MaliyetYontemi))
        {
            throw new BaseException($"{current.MaliYil} mali yılı için stok maliyet yöntemi seçilmelidir.", 400);
        }

        return current.MaliyetYontemi;
    }

    private static void ValidateRequest(UpsertStokMaliyetPolitikasiRequest request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);
        }

        if (request.MaliYil < 2000 || request.MaliYil > 2100)
        {
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);
        }

        if (!StokMaliyetYontemleri.All.Contains(request.MaliyetYontemi, StringComparer.Ordinal))
        {
            throw new BaseException("Geçersiz stok maliyet yöntemi seçildi.", 400);
        }

        if (!string.Equals(request.MaliyetYontemi, StokMaliyetYontemleri.AgirlikliOrtalama, StringComparison.Ordinal)
            && !string.Equals(request.MaliyetYontemi, StokMaliyetYontemleri.FIFO, StringComparison.Ordinal))
        {
            throw new BaseException("Seçilen stok maliyet yöntemi henüz desteklenmiyor.", 400);
        }
    }

    private async Task<int> ResolveMaliYilAsync(int tesisId, DateTime tarih, CancellationToken cancellationToken)
    {
        var donem = await _muhasebeDonemService.GetDonemByTarihAsync(tesisId, tarih, cancellationToken);
        if (donem is null)
        {
            throw new BaseException("Bu tarih için muhasebe dönemi tanımlanmamıştır.", 400);
        }

        return donem.MaliYil;
    }

    private async Task<bool> HasCostSnapshottedMovementAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        return await _dbContext.StokHareketleri
            .AnyAsync(x =>
                x.Durum == STYS.Muhasebe.StokHareketleri.Entities.StokHareketDurumlari.Aktif &&
                (x.MaliyetBirimFiyat != null || x.MaliyetTutari != null) &&
                x.Depo != null &&
                x.Depo.TesisId == tesisId &&
                _dbContext.MuhasebeDonemler.Any(d =>
                    d.TesisId == tesisId &&
                    d.MaliYil == maliYil &&
                    d.BaslangicTarihi <= x.HareketTarihi &&
                    d.BitisTarihi >= x.HareketTarihi),
                cancellationToken);
    }

    private async Task<bool> HasOpenFifoLayersAsync(int tesisId, CancellationToken cancellationToken)
    {
        return await _dbContext.StokMaliyetKatmanlari
            .AsNoTracking()
            .AnyAsync(x =>
                x.TesisId == tesisId &&
                x.KalanMiktar > 0,
                cancellationToken);
    }

    private static StokMaliyetPolitikasiDto Map(StokMaliyetPolitikasi politika)
    {
        return new StokMaliyetPolitikasiDto
        {
            Id = politika.Id,
            TesisId = politika.TesisId,
            MaliYil = politika.MaliYil,
            MaliyetYontemi = politika.MaliyetYontemi
        };
    }
}

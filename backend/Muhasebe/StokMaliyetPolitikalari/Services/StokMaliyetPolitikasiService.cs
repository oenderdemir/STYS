using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class StokMaliyetPolitikasiService : IStokMaliyetPolitikasiService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IStokHareketRepository _stokHareketRepository;

    public StokMaliyetPolitikasiService(
        StysAppDbContext dbContext,
        IMuhasebeDonemService muhasebeDonemService,
        IMuhasebeTesisScopeService tesisScopeService,
        IUserAccessScopeService userAccessScopeService,
        IStokHareketRepository stokHareketRepository)
    {
        _dbContext = dbContext;
        _muhasebeDonemService = muhasebeDonemService;
        _tesisScopeService = tesisScopeService;
        _userAccessScopeService = userAccessScopeService;
        _stokHareketRepository = stokHareketRepository;
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

    public async Task<List<FifoBaslangicStoguSatirDto>> GetFifoBaslangicStoguAsync(int tesisId, int maliYil, CancellationToken cancellationToken = default)
    {
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        await EnsureFifoPolicyAsync(tesisId, maliYil, cancellationToken);

        var allowedDepoIds = await ResolveAllowedDepoIdsAsync(tesisId, cancellationToken);
        if (allowedDepoIds.Count == 0)
        {
            return [];
        }

        return await BuildFifoBaslangicRowsAsync(allowedDepoIds, cancellationToken);
    }

    public async Task<List<FifoBaslangicStoguSatirDto>> CreateFifoBaslangicStoguAsync(CreateFifoBaslangicStoguRequest request, CancellationToken cancellationToken = default)
    {
        ValidateFifoBaslangicRequest(request);
        await _tesisScopeService.EnsureCanAccessTesisAsync(request.TesisId, cancellationToken);
        await EnsureFifoPolicyAsync(request.TesisId, request.MaliYil, cancellationToken);

        var allowedDepoIds = await ResolveAllowedDepoIdsAsync(request.TesisId, cancellationToken);
        if (allowedDepoIds.Count == 0)
        {
            return [];
        }

        var satirlar = request.Satirlar
            .GroupBy(x => new { x.DepoId, x.TasinirKartId })
            .Select(g =>
            {
                if (g.Count() > 1)
                {
                    throw new BaseException("Aynı depo ve taşınır kart için başlangıç maliyeti birden fazla kez gönderilemez.", 400);
                }

                return g.Single();
            })
            .ToList();

        var maliYilBaslangici = await GetMaliYilBaslangiciAsync(request.TesisId, request.MaliYil, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            foreach (var satir in satirlar)
            {
                if (!allowedDepoIds.Contains(satir.DepoId))
                {
                    throw new BaseException("Seçilen depo için yetkiniz bulunmuyor.", 403);
                }

                if (satir.BirimMaliyet < 0)
                {
                    throw new BaseException("Başlangıç birim maliyeti negatif olamaz.", 400);
                }

                var depo = await _dbContext.Depolar
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == satir.DepoId && x.TesisId == request.TesisId, cancellationToken)
                    ?? throw new BaseException("Seçilen depo tesis ile uyumlu değil.", 400);

                var tasinirKart = await _dbContext.TasinirKartlar
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == satir.TasinirKartId && x.TesisId == request.TesisId, cancellationToken)
                    ?? throw new BaseException("Seçilen taşınır kart tesis ile uyumlu değil.", 400);

                var mevcutStokMiktari = await _stokHareketRepository.GetBakiyeMiktariAsync(satir.DepoId, satir.TasinirKartId, cancellationToken);
                var fifoKatmanMiktari = await GetOpenFifoKatmanMiktariAsync(satir.DepoId, satir.TasinirKartId, cancellationToken);
                var katmansizMiktar = mevcutStokMiktari - fifoKatmanMiktari;
                if (katmansizMiktar <= 0)
                {
                    continue;
                }

                if (mevcutStokMiktari <= 0)
                {
                    throw new BaseException("Başlangıç katmanı oluşturulacak satırda mevcut stok bulunmalıdır.", 400);
                }

                _dbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
                {
                    TesisId = depo.TesisId!.Value,
                    DepoId = depo.Id,
                    TasinirKartId = tasinirKart.Id,
                    KaynakStokHareketId = null,
                    KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.BaslangicStogu,
                    GirisTarihi = maliYilBaslangici,
                    IlkMiktar = katmansizMiktar,
                    KalanMiktar = katmansizMiktar,
                    BirimMaliyet = satir.BirimMaliyet
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await GetFifoBaslangicStoguAsync(request.TesisId, request.MaliYil, cancellationToken);
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

    private static void ValidateFifoBaslangicRequest(CreateFifoBaslangicStoguRequest request)
    {
        if (request.TesisId <= 0)
        {
            throw new BaseException("Geçerli bir tesis seçilmelidir.", 400);
        }

        if (request.MaliYil < 2000 || request.MaliYil > 2100)
        {
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);
        }

        if (request.Satirlar.Count == 0)
        {
            throw new BaseException("FIFO başlangıç stoğu için en az bir satır seçilmelidir.", 400);
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

    private async Task<HashSet<int>> ResolveAllowedDepoIdsAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _dbContext.Depolar
            .AsNoTracking()
            .Where(x => x.TesisId == tesisId);

        if (scope.IsScoped)
        {
            query = query.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
        }

        return (await query.Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();
    }

    private async Task EnsureFifoPolicyAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var politika = await _dbContext.StokMaliyetPolitikalari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TesisId == tesisId && x.MaliYil == maliYil, cancellationToken);

        if (politika is null || !string.Equals(politika.MaliyetYontemi, StokMaliyetYontemleri.FIFO, StringComparison.Ordinal))
        {
            throw new BaseException("FIFO başlangıç stoğu yalnızca FIFO maliyet politikası seçiliyse oluşturulabilir.", 400);
        }
    }

    private async Task<DateTime> GetMaliYilBaslangiciAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var baslangic = await _dbContext.MuhasebeDonemler
            .AsNoTracking()
            .Where(x => x.TesisId == tesisId && x.MaliYil == maliYil && x.IsDeleted == false)
            .OrderBy(x => x.BaslangicTarihi)
            .Select(x => (DateTime?)x.BaslangicTarihi)
            .FirstOrDefaultAsync(cancellationToken);

        return baslangic ?? throw new BaseException("Bu mali yıl için muhasebe dönemi tanımlanmamıştır.", 400);
    }

    private async Task<List<FifoBaslangicStoguSatirDto>> BuildFifoBaslangicRowsAsync(HashSet<int> allowedDepoIds, CancellationToken cancellationToken)
    {
        var stokBakiyeleri = await _stokHareketRepository.GetDepoStokBakiyeleriAsync(allowedDepoIds, cancellationToken);
        if (stokBakiyeleri.Count == 0)
        {
            return [];
        }

        var legacyDegerleme = await _stokHareketRepository.GetStokDegerlemeAsync(allowedDepoIds, cancellationToken);
        var katmanBakiyeleri = await _dbContext.StokMaliyetKatmanlari
            .AsNoTracking()
            .Where(x => allowedDepoIds.Contains(x.DepoId) && x.KalanMiktar > 0)
            .GroupBy(x => new { x.DepoId, x.TasinirKartId })
            .Select(g => new
            {
                g.Key.DepoId,
                g.Key.TasinirKartId,
                FifoKatmanMiktari = g.Sum(x => x.KalanMiktar)
            })
            .ToListAsync(cancellationToken);

        var katmanMap = katmanBakiyeleri.ToDictionary(x => (x.DepoId, x.TasinirKartId), x => x.FifoKatmanMiktari);
        var degerlemeMap = legacyDegerleme.ToDictionary(x => (x.DepoId, x.TasinirKartId));

        return stokBakiyeleri
            .Select(item =>
            {
                var fifoKatmanMiktari = katmanMap.TryGetValue((item.DepoId, item.TasinirKartId), out var katmanMiktari)
                    ? katmanMiktari
                    : 0m;
                var katmansizMiktar = item.BakiyeMiktari - fifoKatmanMiktari;
                degerlemeMap.TryGetValue((item.DepoId, item.TasinirKartId), out var degerleme);

                var maliyetGuvenilirMi = degerleme is not null && !degerleme.MaliyetEksikMi;
                return new FifoBaslangicStoguSatirDto
                {
                    DepoId = item.DepoId,
                    DepoKod = item.DepoKod,
                    DepoAd = item.DepoAd,
                    TasinirKartId = item.TasinirKartId,
                    StokKodu = item.StokKodu,
                    TasinirKartAd = item.TasinirKartAd,
                    Birim = item.Birim,
                    MevcutStokMiktari = item.BakiyeMiktari,
                    FifoKatmanMiktari = fifoKatmanMiktari,
                    KatmansizMiktar = katmansizMiktar,
                    OnerilenBirimMaliyet = maliyetGuvenilirMi ? degerleme!.OrtalamaMaliyet : null,
                    MaliyetGuvenilirMi = maliyetGuvenilirMi
                };
            })
            .Where(x => x.KatmansizMiktar > 0)
            .OrderBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ToList();
    }

    private async Task<decimal> GetOpenFifoKatmanMiktariAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken)
    {
        return await _dbContext.StokMaliyetKatmanlari
            .Where(x => x.DepoId == depoId && x.TasinirKartId == tasinirKartId && x.KalanMiktar > 0)
            .SumAsync(x => (decimal?)x.KalanMiktar, cancellationToken) ?? 0m;
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

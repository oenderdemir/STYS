using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class StokMaliyetKatmaniRestoreService : IStokMaliyetKatmaniRestoreService
{
    private readonly StysAppDbContext _dbContext;

    public StokMaliyetKatmaniRestoreService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RestoreLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto reversalMovement, CancellationToken cancellationToken = default)
    {
        var hasConsumptions = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .AnyAsync(x => x.CikisStokHareketId == originalMovement.Id && !x.IsDeleted, cancellationToken);

        if (!hasConsumptions)
        {
            return;
        }

        var maliyetYontemi = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .Where(x => x.CikisStokHareketId == originalMovement.Id && !x.IsDeleted)
            .Select(x => x.StokMaliyetKatmani!.MaliyetYontemi)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Geri alma için maliyet katmanı bilgisi bulunamadı.", 400);

        LayeredCostStrategyBase strategy = maliyetYontemi switch
        {
            StokMaliyetYontemleri.FIFO => new FifoMaliyetStrategy(_dbContext),
            StokMaliyetYontemleri.LIFO => new LifoMaliyetStrategy(_dbContext),
            _ => throw new BaseException("Bu işlem için maliyet katmanı geri yükleme uygulanamaz.", 400)
        };

        await strategy.RestoreOutgoingConsumptionAsIncomingLayersAsync(
            originalMovement.Id,
            reversalMovement.Id!.Value,
            reversalMovement.DepoId,
            reversalMovement.TasinirKartId,
            reversalMovement.HareketTarihi,
            cancellationToken);
    }

    public async Task<StokMaliyetRestorePlan?> PlanPartialRestoreAsync(
        int originalMovementId,
        decimal alreadyRestoredQuantity,
        decimal returnQuantity,
        CancellationToken cancellationToken = default)
    {
        if (returnQuantity <= 0)
        {
            return null;
        }

        var hasConsumptions = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .AnyAsync(x => x.CikisStokHareketId == originalMovementId && !x.IsDeleted, cancellationToken);

        if (!hasConsumptions)
        {
            // Weighted-average: layer üretilmez; maliyet orijinal hareketin snapshot'ından taşınır.
            return null;
        }

        var maliyetYontemi = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .Where(x => x.CikisStokHareketId == originalMovementId && !x.IsDeleted)
            .Select(x => x.StokMaliyetKatmani!.MaliyetYontemi)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("İade için maliyet katmanı bilgisi bulunamadı.", 400);

        LayeredCostStrategyBase strategy = maliyetYontemi switch
        {
            StokMaliyetYontemleri.FIFO => new FifoMaliyetStrategy(_dbContext),
            StokMaliyetYontemleri.LIFO => new LifoMaliyetStrategy(_dbContext),
            _ => throw new BaseException("Bu işlem için maliyet katmanı geri yükleme uygulanamaz.", 400)
        };

        var segmentler = await strategy.ComputePartialRestoreSegmentsAsync(
            originalMovementId,
            alreadyRestoredQuantity,
            returnQuantity,
            cancellationToken);

        var toplamMaliyet = segmentler.Sum(x => x.Tutar);
        var efektifBirimMaliyet = returnQuantity > 0
            ? Math.Round(toplamMaliyet / returnQuantity, 6, MidpointRounding.AwayFromZero)
            : 0m;

        return new StokMaliyetRestorePlan(maliyetYontemi, segmentler, toplamMaliyet, efektifBirimMaliyet);
    }

    public async Task RestorePlannedLayersAsync(StokMaliyetRestorePlan plan, StokHareketDto iadeMovement, CancellationToken cancellationToken = default)
    {
        LayeredCostStrategyBase strategy = plan.MaliyetYontemi switch
        {
            StokMaliyetYontemleri.FIFO => new FifoMaliyetStrategy(_dbContext),
            StokMaliyetYontemleri.LIFO => new LifoMaliyetStrategy(_dbContext),
            _ => throw new BaseException("Bu işlem için maliyet katmanı geri yükleme uygulanamaz.", 400)
        };

        await strategy.RestorePlannedSegmentsAsIncomingLayersAsync(
            plan.Segmentler,
            iadeMovement.Id!.Value,
            iadeMovement.DepoId,
            iadeMovement.TasinirKartId,
            iadeMovement.HareketTarihi,
            cancellationToken);
    }
}

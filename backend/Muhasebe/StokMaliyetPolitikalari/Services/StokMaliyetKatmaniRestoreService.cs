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

    public async Task RestorePartialLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto iadeMovement, decimal iadeMiktari, CancellationToken cancellationToken = default)
    {
        if (iadeMiktari <= 0)
        {
            return;
        }

        var hasConsumptions = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .AnyAsync(x => x.CikisStokHareketId == originalMovement.Id && !x.IsDeleted, cancellationToken);

        if (!hasConsumptions)
        {
            // Weighted-average: layer üretilmez; maliyet snapshot iade hareketinin üzerinde taşınır.
            return;
        }

        var maliyetYontemi = await _dbContext.StokMaliyetKatmanTuketimleri
            .AsNoTracking()
            .Where(x => x.CikisStokHareketId == originalMovement.Id && !x.IsDeleted)
            .Select(x => x.StokMaliyetKatmani!.MaliyetYontemi)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("İade için maliyet katmanı bilgisi bulunamadı.", 400);

        LayeredCostStrategyBase strategy = maliyetYontemi switch
        {
            StokMaliyetYontemleri.FIFO => new FifoMaliyetStrategy(_dbContext),
            StokMaliyetYontemleri.LIFO => new LifoMaliyetStrategy(_dbContext),
            _ => throw new BaseException("Bu işlem için maliyet katmanı geri yükleme uygulanamaz.", 400)
        };

        await strategy.AddPartialIncomingLayerAsync(
            iadeMovement.Id!.Value,
            iadeMovement.DepoId,
            iadeMovement.TasinirKartId,
            iadeMovement.HareketTarihi,
            iadeMiktari,
            originalMovement.MaliyetBirimFiyat ?? 0m,
            cancellationToken);
    }
}

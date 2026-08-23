using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class LifoMaliyetStrategy : LayeredCostStrategyBase
{
    public LifoMaliyetStrategy(StysAppDbContext dbContext)
        : base(dbContext)
    {
    }

    public override string MaliyetYontemi => StokMaliyetYontemleri.LIFO;

    protected override IOrderedQueryable<StokMaliyetKatmani> ApplyLayerOrdering(IQueryable<StokMaliyetKatmani> query)
        => query
            .OrderByDescending(x => x.GirisTarihi)
            .ThenByDescending(x => x.KaynakStokHareketId ?? 0)
            .ThenByDescending(x => x.Id);
}

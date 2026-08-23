using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public interface IStokMaliyetStrategy
{
    string MaliyetYontemi { get; }
    Task ApplyCostSnapshotAsync(StokHareketDto dto, StokHareket? existing, CancellationToken cancellationToken = default);
    Task<StokCostSnapshot> GetCurrentCostSnapshotAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default);
}

public interface IStokMaliyetStrategyResolver
{
    IStokMaliyetStrategy Resolve(string maliyetYontemi);
}

public readonly record struct StokCostSnapshot(decimal BakiyeMiktari, decimal ToplamStokDegeri, decimal OrtalamaMaliyet);

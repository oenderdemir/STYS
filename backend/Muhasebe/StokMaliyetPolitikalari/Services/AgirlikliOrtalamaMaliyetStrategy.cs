using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class AgirlikliOrtalamaMaliyetStrategy : IStokMaliyetStrategy
{
    private readonly StysAppDbContext _dbContext;

    public AgirlikliOrtalamaMaliyetStrategy(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MaliyetYontemi => Dtos.StokMaliyetYontemleri.AgirlikliOrtalama;

    public async Task ApplyCostSnapshotAsync(StokHareketDto dto, StokHareket? existing, CancellationToken cancellationToken = default)
    {
        if (!StokHareketTipleri.IsGirisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu)
            && !StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu))
        {
            dto.MaliyetBirimFiyat = null;
            dto.MaliyetTutari = null;
            return;
        }

        var baseSnapshot = await GetBaseCostSnapshotForDtoAsync(dto, existing, cancellationToken);
        if (StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu))
        {
            dto.MaliyetBirimFiyat = baseSnapshot.OrtalamaMaliyet;
            dto.MaliyetTutari = CalculateCostAmount(dto.Miktar, baseSnapshot.OrtalamaMaliyet);
            return;
        }

        if (string.Equals(dto.HareketTipi, StokHareketTipleri.SayimFarki, StringComparison.Ordinal)
            && string.Equals(dto.SayimFarkiYonu, StokSayimFarkiYonleri.Fazla, StringComparison.Ordinal))
        {
            dto.MaliyetBirimFiyat = baseSnapshot.OrtalamaMaliyet;
            dto.MaliyetTutari = CalculateCostAmount(dto.Miktar, baseSnapshot.OrtalamaMaliyet);
            return;
        }

        dto.MaliyetBirimFiyat = dto.BirimFiyat;
        dto.MaliyetTutari = CalculateCostAmount(dto.Miktar, dto.BirimFiyat);
    }

    public async Task<StokCostSnapshot> GetCurrentCostSnapshotAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.StokHareketleri
            .AsNoTracking()
            .Where(x =>
                x.DepoId == depoId &&
                x.TasinirKartId == tasinirKartId &&
                x.Durum == StokHareketDurumlari.Aktif)
            .Select(x => new
            {
                x.HareketTipi,
                x.TransferYonu,
                x.SayimFarkiYonu,
                x.Miktar,
                x.BirimFiyat,
                x.MaliyetTutari,
                x.Durum
            })
            .ToListAsync(cancellationToken);

        var bakiyeMiktari = rows.Sum(x => CalculateMovementEffect(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar, x.Durum));
        var toplamStokDegeri = rows.Sum(x => CalculateCostValueEffect(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar, x.MaliyetTutari, x.BirimFiyat, x.Durum));
        return CreateStockCostSnapshot(bakiyeMiktari, toplamStokDegeri);
    }

    private async Task<StokCostSnapshot> GetBaseCostSnapshotForDtoAsync(StokHareketDto dto, StokHareket? existing, CancellationToken cancellationToken)
    {
        var currentSnapshot = await GetCurrentCostSnapshotAsync(dto.DepoId, dto.TasinirKartId, cancellationToken);
        if (existing is null || existing.DepoId != dto.DepoId || existing.TasinirKartId != dto.TasinirKartId)
        {
            return currentSnapshot;
        }

        var baseBakiye = currentSnapshot.BakiyeMiktari - CalculateMovementEffect(existing);
        var baseDeger = currentSnapshot.ToplamStokDegeri - CalculateCostValueEffect(existing);
        return CreateStockCostSnapshot(baseBakiye, baseDeger);
    }

    private static StokCostSnapshot CreateStockCostSnapshot(decimal bakiyeMiktari, decimal toplamStokDegeri)
    {
        var ortalamaMaliyet = bakiyeMiktari <= 0
            ? 0m
            : Math.Round(toplamStokDegeri / bakiyeMiktari, 6, MidpointRounding.AwayFromZero);

        return new StokCostSnapshot(bakiyeMiktari, toplamStokDegeri, ortalamaMaliyet);
    }

    private static decimal CalculateMovementEffect(StokHareket existing)
        => CalculateMovementEffect(existing.HareketTipi, existing.TransferYonu, existing.SayimFarkiYonu, existing.Miktar, existing.Durum);

    private static decimal CalculateCostValueEffect(StokHareket existing)
        => CalculateCostValueEffect(existing.HareketTipi, existing.TransferYonu, existing.SayimFarkiYonu, existing.Miktar, existing.MaliyetTutari, existing.BirimFiyat, existing.Durum);

    private static decimal CalculateCostValueEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar, decimal? maliyetTutari, decimal birimFiyat, string? durum)
    {
        if (!string.Equals(durum, StokHareketDurumlari.Aktif, StringComparison.Ordinal))
        {
            return 0m;
        }

        if (StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return maliyetTutari ?? CalculateCostAmount(miktar, birimFiyat);
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return -(maliyetTutari ?? 0m);
        }

        return 0m;
    }

    private static decimal CalculateMovementEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar, string? durum)
    {
        if (!string.Equals(durum, StokHareketDurumlari.Aktif, StringComparison.Ordinal))
        {
            return 0m;
        }

        if (StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return miktar;
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return -miktar;
        }

        return 0m;
    }

    private static decimal CalculateCostAmount(decimal miktar, decimal birimFiyat)
        => Math.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
}

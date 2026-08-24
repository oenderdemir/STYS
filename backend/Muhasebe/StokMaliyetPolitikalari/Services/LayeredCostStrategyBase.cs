using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public abstract class LayeredCostStrategyBase : IStokMaliyetStrategy
{
    private const string MissingLayerMessage = "Mevcut stok için maliyet katmanı bulunmuyor. Maliyet başlangıç stoğu oluşturulmalıdır.";
    protected readonly StysAppDbContext DbContext;

    protected LayeredCostStrategyBase(StysAppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    public abstract string MaliyetYontemi { get; }

    public async Task ApplyCostSnapshotAsync(StokHareketDto dto, StokHareket? existing, CancellationToken cancellationToken = default)
    {
        if (!IsCostSensitiveMovement(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu))
        {
            dto.MaliyetBirimFiyat = null;
            dto.MaliyetTutari = null;
            return;
        }

        if (StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu))
        {
            var plan = await PlanOutgoingConsumptionAsync(dto.DepoId, dto.TasinirKartId, dto.Miktar, cancellationToken);
            dto.MaliyetTutari = plan.ToplamMaliyet;
            dto.MaliyetBirimFiyat = dto.Miktar <= 0
                ? 0m
                : Math.Round(plan.ToplamMaliyet / dto.Miktar, 6, MidpointRounding.AwayFromZero);
            return;
        }

        if (string.Equals(dto.HareketTipi, StokHareketTipleri.SayimFarki, StringComparison.Ordinal)
            && string.Equals(dto.SayimFarkiYonu, StokSayimFarkiYonleri.Fazla, StringComparison.Ordinal))
        {
            var snapshot = await GetCurrentCostSnapshotAsync(dto.DepoId, dto.TasinirKartId, cancellationToken);
            dto.MaliyetBirimFiyat = snapshot.OrtalamaMaliyet;
            dto.MaliyetTutari = CalculateCostAmount(dto.Miktar, snapshot.OrtalamaMaliyet);
            return;
        }

        dto.MaliyetBirimFiyat = dto.BirimFiyat;
        dto.MaliyetTutari = CalculateCostAmount(dto.Miktar, dto.BirimFiyat);
    }

    public async Task<StokCostSnapshot> GetCurrentCostSnapshotAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var katmanlar = await DbContext.StokMaliyetKatmanlari
            .AsNoTracking()
            .Where(x =>
                x.DepoId == depoId &&
                x.TasinirKartId == tasinirKartId &&
                x.KalanMiktar > 0)
            .Select(x => new
            {
                x.KalanMiktar,
                x.BirimMaliyet
            })
            .ToListAsync(cancellationToken);

        var bakiyeMiktari = katmanlar.Sum(x => x.KalanMiktar);
        var toplamStokDegeri = katmanlar.Sum(x => x.KalanMiktar * x.BirimMaliyet);
        var ortalamaMaliyet = bakiyeMiktari <= 0
            ? 0m
            : Math.Round(toplamStokDegeri / bakiyeMiktari, 6, MidpointRounding.AwayFromZero);

        return new StokCostSnapshot(bakiyeMiktari, toplamStokDegeri, ortalamaMaliyet);
    }

    public async Task ApplyCreatedMovementAsync(StokHareket hareket, CancellationToken cancellationToken = default)
    {
        if (StokHareketTipleri.IsGirisEtkisi(hareket.HareketTipi, hareket.TransferYonu, hareket.SayimFarkiYonu))
        {
            await CreateIncomingLayerAsync(
                hareket.Id,
                hareket.DepoId,
                hareket.TasinirKartId,
                hareket.HareketTarihi,
                hareket.Miktar,
                hareket.MaliyetBirimFiyat ?? 0m,
                cancellationToken);
            return;
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareket.HareketTipi, hareket.TransferYonu, hareket.SayimFarkiYonu))
        {
            var plan = await PlanOutgoingConsumptionAsync(hareket.DepoId, hareket.TasinirKartId, hareket.Miktar, cancellationToken);
            await ApplyConsumptionPlanAsync(hareket.Id, plan, cancellationToken);
        }
    }

    public async Task ApplyTransferAsync(StokHareket kaynakHareket, StokHareket hedefHareket, CancellationToken cancellationToken = default)
    {
        var plan = await PlanOutgoingConsumptionAsync(kaynakHareket.DepoId, kaynakHareket.TasinirKartId, kaynakHareket.Miktar, cancellationToken);
        await ApplyConsumptionPlanAsync(kaynakHareket.Id, plan, cancellationToken);

        foreach (var tuketim in plan.Kalemler)
        {
            await CreateIncomingLayerAsync(
                hedefHareket.Id,
                hedefHareket.DepoId,
                hedefHareket.TasinirKartId,
                hedefHareket.HareketTarihi,
                tuketim.Miktar,
                tuketim.BirimMaliyet,
                cancellationToken);
        }
    }

    public async Task ReverseOutgoingConsumptionAsync(int cikisStokHareketId, CancellationToken cancellationToken = default)
    {
        var tuketimler = await DbContext.StokMaliyetKatmanTuketimleri
            .Where(x => x.CikisStokHareketId == cikisStokHareketId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var tuketim in tuketimler)
        {
            var katman = await DbContext.StokMaliyetKatmanlari
                .FirstAsync(x => x.Id == tuketim.StokMaliyetKatmaniId, cancellationToken);

            katman.KalanMiktar += tuketim.Miktar;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LayeredConsumptionPlan> PlanOutgoingConsumptionAsync(int depoId, int tasinirKartId, decimal miktar, CancellationToken cancellationToken = default)
    {
        if (miktar <= 0)
        {
            return new LayeredConsumptionPlan([], 0m);
        }

        var currentBalance = await GetCurrentStockBalanceAsync(depoId, tasinirKartId, cancellationToken);
        var openLayers = await ApplyLayerOrdering(DbContext.StokMaliyetKatmanlari
                .Where(x =>
                    x.DepoId == depoId &&
                    x.TasinirKartId == tasinirKartId &&
                    x.KalanMiktar > 0))
            .ToListAsync(cancellationToken);

        var layerBalance = openLayers.Sum(x => x.KalanMiktar);
        if (currentBalance > 0 && layerBalance < currentBalance)
        {
            throw new BaseException(MissingLayerMessage, 400);
        }

        if (layerBalance < miktar)
        {
            throw new BaseException("Depoda bu işlem için yeterli stok bulunmamaktadır.", 400);
        }

        var remaining = miktar;
        var kalemler = new List<LayeredConsumptionItem>();
        foreach (var layer in openLayers)
        {
            if (remaining <= 0)
            {
                break;
            }

            var consume = Math.Min(layer.KalanMiktar, remaining);
            if (consume <= 0)
            {
                continue;
            }

            kalemler.Add(new LayeredConsumptionItem(
                layer.Id,
                consume,
                layer.BirimMaliyet,
                CalculateCostAmount(consume, layer.BirimMaliyet)));
            remaining -= consume;
        }

        if (remaining > 0)
        {
            throw new BaseException("Depoda bu işlem için yeterli stok bulunmamaktadır.", 400);
        }

        return new LayeredConsumptionPlan(kalemler, kalemler.Sum(x => x.Tutar));
    }

    protected abstract IOrderedQueryable<StokMaliyetKatmani> ApplyLayerOrdering(IQueryable<StokMaliyetKatmani> query);

    private async Task ApplyConsumptionPlanAsync(int cikisStokHareketId, LayeredConsumptionPlan plan, CancellationToken cancellationToken)
    {
        foreach (var item in plan.Kalemler)
        {
            var katman = await DbContext.StokMaliyetKatmanlari
                .FirstAsync(x => x.Id == item.StokMaliyetKatmaniId, cancellationToken);

            katman.KalanMiktar -= item.Miktar;
            DbContext.StokMaliyetKatmanTuketimleri.Add(new StokMaliyetKatmanTuketimi
            {
                CikisStokHareketId = cikisStokHareketId,
                StokMaliyetKatmaniId = katman.Id,
                Miktar = item.Miktar,
                BirimMaliyet = item.BirimMaliyet,
                Tutar = item.Tutar
            });
        }

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateIncomingLayerAsync(
        int kaynakStokHareketId,
        int depoId,
        int tasinirKartId,
        DateTime girisTarihi,
        decimal miktar,
        decimal birimMaliyet,
        CancellationToken cancellationToken)
    {
        var depo = await DbContext.Depolar
            .AsNoTracking()
            .FirstAsync(x => x.Id == depoId, cancellationToken);

        DbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
        {
            TesisId = depo.TesisId!.Value,
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            KaynakStokHareketId = kaynakStokHareketId,
            KatmanKaynakTipi = StokMaliyetKatmanKaynakTipleri.StokHareketi,
            MaliyetYontemi = MaliyetYontemi,
            GirisTarihi = girisTarihi,
            IlkMiktar = miktar,
            KalanMiktar = miktar,
            BirimMaliyet = birimMaliyet
        });
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<decimal> GetCurrentStockBalanceAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken)
    {
        var rows = await DbContext.StokHareketleri
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
                x.Miktar
            })
            .ToListAsync(cancellationToken);

        return rows.Sum(x =>
        {
            if (StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
            {
                return x.Miktar;
            }

            if (StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
            {
                return -x.Miktar;
            }

            return 0m;
        });
    }

    private static bool IsCostSensitiveMovement(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu)
        => StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu)
           || StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu);

    protected static decimal CalculateCostAmount(decimal miktar, decimal birimMaliyet)
        => Math.Round(miktar * birimMaliyet, 2, MidpointRounding.AwayFromZero);
}

public sealed record LayeredConsumptionPlan(IReadOnlyList<LayeredConsumptionItem> Kalemler, decimal ToplamMaliyet);
public sealed record LayeredConsumptionItem(int StokMaliyetKatmaniId, decimal Miktar, decimal BirimMaliyet, decimal Tutar);

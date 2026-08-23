using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public class FifoMaliyetStrategy : IStokMaliyetStrategy
{
    private readonly StysAppDbContext _dbContext;

    public FifoMaliyetStrategy(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string MaliyetYontemi => Dtos.StokMaliyetYontemleri.FIFO;

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
            // Sayım farkı fazla girişinde kullanıcı maliyeti ayrı vermiyor. Bu yüzden açık FIFO
            // katmanlarının güncel ortalamasını kullanıyoruz; hiç katman yoksa 0 maliyet açıyoruz.
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
        var katmanlar = await _dbContext.StokMaliyetKatmanlari
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

    public async Task<FifoConsumptionPlan> PlanOutgoingConsumptionAsync(int depoId, int tasinirKartId, decimal miktar, CancellationToken cancellationToken = default)
    {
        if (miktar <= 0)
        {
            return new FifoConsumptionPlan([], 0m);
        }

        var currentBalance = await GetCurrentStockBalanceAsync(depoId, tasinirKartId, cancellationToken);
        var openLayers = await _dbContext.StokMaliyetKatmanlari
            .Where(x =>
                x.DepoId == depoId &&
                x.TasinirKartId == tasinirKartId &&
                x.KalanMiktar > 0)
            .OrderBy(x => x.GirisTarihi)
            .ThenBy(x => x.KaynakStokHareketId)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var layerBalance = openLayers.Sum(x => x.KalanMiktar);
        if (currentBalance > 0 && layerBalance < currentBalance)
        {
            throw new BaseException("Mevcut stok için FIFO maliyet katmanı bulunmuyor. FIFO başlangıç stoğu oluşturulmalıdır.", 400);
        }

        if (layerBalance < miktar)
        {
            throw new BaseException("Depoda bu işlem için yeterli stok bulunmamaktadır.", 400);
        }

        var remaining = miktar;
        var kalemler = new List<FifoConsumptionItem>();
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

            kalemler.Add(new FifoConsumptionItem(
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

        return new FifoConsumptionPlan(kalemler, kalemler.Sum(x => x.Tutar));
    }

    private async Task ApplyConsumptionPlanAsync(int cikisStokHareketId, FifoConsumptionPlan plan, CancellationToken cancellationToken)
    {
        foreach (var item in plan.Kalemler)
        {
            var katman = await _dbContext.StokMaliyetKatmanlari
                .FirstAsync(x => x.Id == item.StokMaliyetKatmaniId, cancellationToken);

            katman.KalanMiktar -= item.Miktar;
            _dbContext.StokMaliyetKatmanTuketimleri.Add(new StokMaliyetKatmanTuketimi
            {
                CikisStokHareketId = cikisStokHareketId,
                StokMaliyetKatmaniId = katman.Id,
                Miktar = item.Miktar,
                BirimMaliyet = item.BirimMaliyet,
                Tutar = item.Tutar
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
        var depo = await _dbContext.Depolar
            .AsNoTracking()
            .FirstAsync(x => x.Id == depoId, cancellationToken);

        _dbContext.StokMaliyetKatmanlari.Add(new StokMaliyetKatmani
        {
            TesisId = depo.TesisId!.Value,
            DepoId = depoId,
            TasinirKartId = tasinirKartId,
            KaynakStokHareketId = kaynakStokHareketId,
            GirisTarihi = girisTarihi,
            IlkMiktar = miktar,
            KalanMiktar = miktar,
            BirimMaliyet = birimMaliyet
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<decimal> GetCurrentStockBalanceAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken)
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

    private static decimal CalculateCostAmount(decimal miktar, decimal birimMaliyet)
        => Math.Round(miktar * birimMaliyet, 2, MidpointRounding.AwayFromZero);
}

public sealed record FifoConsumptionPlan(IReadOnlyList<FifoConsumptionItem> Kalemler, decimal ToplamMaliyet);
public sealed record FifoConsumptionItem(int StokMaliyetKatmaniId, decimal Miktar, decimal BirimMaliyet, decimal Tutar);

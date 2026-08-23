using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokUyarilari.Dtos;

namespace STYS.Muhasebe.StokUyarilari.Services;

public class StokUyariService : IStokUyariService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public StokUyariService(
        StysAppDbContext dbContext,
        IMuhasebeTesisScopeService tesisScopeService,
        IUserAccessScopeService userAccessScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<List<StokUyariDto>> GetStokUyarilariAsync(int tesisId, int? depoId, int? tasinirKartId, bool sadeceRiskli, CancellationToken cancellationToken = default)
    {
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var allowedDepoIds = await ResolveAllowedDepoIdsAsync(tesisId, cancellationToken);
        if (allowedDepoIds.Count == 0)
        {
            return [];
        }

        if (depoId.HasValue && depoId.Value > 0)
        {
            if (!allowedDepoIds.Contains(depoId.Value))
            {
                return [];
            }

            allowedDepoIds = [depoId.Value];
        }

        var rows = await _dbContext.StokHareketleri
            .AsNoTracking()
            .Where(x =>
                x.Durum == StokHareketDurumlari.Aktif &&
                allowedDepoIds.Contains(x.DepoId) &&
                x.TasinirKart != null &&
                (x.TasinirKart.MinimumStokMiktari.HasValue || x.TasinirKart.KritikStokMiktari.HasValue) &&
                (!tasinirKartId.HasValue || tasinirKartId.Value <= 0 || x.TasinirKartId == tasinirKartId.Value))
            .Select(x => new
            {
                x.DepoId,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                MinimumStokMiktari = x.TasinirKart != null ? x.TasinirKart.MinimumStokMiktari : null,
                KritikStokMiktari = x.TasinirKart != null ? x.TasinirKart.KritikStokMiktari : null,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new
            {
                x.DepoId,
                x.DepoKod,
                x.DepoAd,
                x.TasinirKartId,
                x.StokKodu,
                x.TasinirKartAd,
                x.MinimumStokMiktari,
                x.KritikStokMiktari
            })
            .Select(g =>
            {
                var mevcutMiktar = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis);
                var durum = ResolveDurum(mevcutMiktar, g.Key.MinimumStokMiktari, g.Key.KritikStokMiktari);

                return new StokUyariDto
                {
                    DepoId = g.Key.DepoId,
                    DepoKod = g.Key.DepoKod,
                    DepoAd = g.Key.DepoAd,
                    TasinirKartId = g.Key.TasinirKartId,
                    StokKodu = g.Key.StokKodu,
                    TasinirKartAd = g.Key.TasinirKartAd,
                    MevcutMiktar = mevcutMiktar,
                    MinimumStokMiktari = g.Key.MinimumStokMiktari,
                    KritikStokMiktari = g.Key.KritikStokMiktari,
                    Durum = durum
                };
            })
            .Where(x => !sadeceRiskli || !string.Equals(x.Durum, StokUyariDurumlari.Normal, StringComparison.Ordinal))
            .OrderBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ThenBy(x => x.TasinirKartId)
            .ToList();
    }

    private async Task<HashSet<int>> ResolveAllowedDepoIdsAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _dbContext.Depolar.AsNoTracking().Where(x => x.TesisId == tesisId);
        if (scope.IsScoped)
        {
            query = query.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
        }

        return (await query.Select(x => x.Id).ToListAsync(cancellationToken)).ToHashSet();
    }

    private static string ResolveDurum(decimal mevcutMiktar, decimal? minimumStokMiktari, decimal? kritikStokMiktari)
    {
        if (kritikStokMiktari.HasValue && mevcutMiktar <= kritikStokMiktari.Value)
        {
            return StokUyariDurumlari.Kritik;
        }

        if (minimumStokMiktari.HasValue && mevcutMiktar <= minimumStokMiktari.Value)
        {
            return StokUyariDurumlari.Dusuk;
        }

        return StokUyariDurumlari.Normal;
    }
}


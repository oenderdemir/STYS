using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Dtos;

namespace STYS.Muhasebe.StokLotlari.Services;

public class StokLotSktUyariService : IStokLotSktUyariService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly TimeProvider _timeProvider;

    public StokLotSktUyariService(
        StysAppDbContext dbContext,
        IMuhasebeTesisScopeService tesisScopeService,
        IUserAccessScopeService userAccessScopeService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
        _userAccessScopeService = userAccessScopeService;
        _timeProvider = timeProvider;
    }

    public async Task<List<StokLotSktUyariDto>> GetSktUyarilariAsync(int tesisId, int? depoId, int? tasinirKartId, bool sadeceRiskli, CancellationToken cancellationToken = default)
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

        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;
        var rows = await _dbContext.StokHareketleri
            .AsNoTracking()
            .Where(x =>
                x.Durum == StokHareketDurumlari.Aktif &&
                allowedDepoIds.Contains(x.DepoId) &&
                x.StokLotId.HasValue &&
                x.StokLot != null &&
                x.StokLot.SonKullanmaTarihi.HasValue &&
                (!tasinirKartId.HasValue || tasinirKartId.Value <= 0 || x.TasinirKartId == tasinirKartId.Value))
            .Select(x => new
            {
                x.DepoId,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                StokLotId = x.StokLotId!.Value,
                LotNo = x.StokLot != null ? x.StokLot.LotNo : string.Empty,
                SonKullanmaTarihi = x.StokLot != null ? x.StokLot.SonKullanmaTarihi!.Value : today,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.DepoId, x.DepoKod, x.DepoAd, x.TasinirKartId, x.StokKodu, x.TasinirKartAd, x.StokLotId, x.LotNo, x.SonKullanmaTarihi })
            .Select(g =>
            {
                var kalanMiktar = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis);
                var kalanGun = (g.Key.SonKullanmaTarihi.Date - today).Days;
                var durum = ResolveDurum(kalanGun);

                return new StokLotSktUyariDto
                {
                    DepoId = g.Key.DepoId,
                    DepoKod = g.Key.DepoKod,
                    DepoAd = g.Key.DepoAd,
                    TasinirKartId = g.Key.TasinirKartId,
                    StokKodu = g.Key.StokKodu,
                    TasinirKartAd = g.Key.TasinirKartAd,
                    StokLotId = g.Key.StokLotId,
                    LotNo = g.Key.LotNo,
                    SonKullanmaTarihi = g.Key.SonKullanmaTarihi,
                    KalanMiktar = kalanMiktar,
                    KalanGun = kalanGun,
                    Durum = durum
                };
            })
            .Where(x => x.KalanMiktar > 0 && (!sadeceRiskli || !string.Equals(x.Durum, StokLotSktUyariDurumlari.Normal, StringComparison.Ordinal)))
            .OrderBy(x => x.SonKullanmaTarihi)
            .ThenBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ThenBy(x => x.LotNo)
            .ToList();
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

    private static string ResolveDurum(int kalanGun)
    {
        if (kalanGun < 0)
        {
            return StokLotSktUyariDurumlari.Gecmis;
        }

        if (kalanGun <= StokLotSktUyariEsikleri.KritikGun)
        {
            return StokLotSktUyariDurumlari.Kritik;
        }

        if (kalanGun <= StokLotSktUyariEsikleri.YaklasiyorGun)
        {
            return StokLotSktUyariDurumlari.Yaklasiyor;
        }

        return StokLotSktUyariDurumlari.Normal;
    }
}

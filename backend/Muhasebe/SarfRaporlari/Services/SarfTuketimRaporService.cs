using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SarfFisleri.Entities;
using STYS.Muhasebe.SarfRaporlari.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SarfRaporlari.Services;

public sealed class SarfTuketimRaporService : ISarfTuketimRaporService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public SarfTuketimRaporService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<PagedResult<SarfTuketimDetayRaporSatirDto>> GetDetayAsync(
        PagedRequest request,
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await ValidateAndEnsureScopeAsync(filter, cancellationToken);

        var (pageNumber, pageSize) = request.Normalize();
        var query = BuildFilteredQuery(filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Tarih)
            .ThenByDescending(x => x.SarfFisiId)
            .ThenByDescending(x => x.SarfFisiSatirId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDetayDto(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<SarfTuketimDetayRaporSatirDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<List<SarfTuketimDetayRaporSatirDto>> GetDetayListAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await ValidateAndEnsureScopeAsync(filter, cancellationToken);

        return await BuildFilteredQuery(filter)
            .OrderByDescending(x => x.Tarih)
            .ThenByDescending(x => x.SarfFisiId)
            .ThenByDescending(x => x.SarfFisiSatirId)
            .Select(x => ToDetayDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SarfTuketimMalzemeOzetDto>> GetMalzemeOzetAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await ValidateAndEnsureScopeAsync(filter, cancellationToken);

        return await BuildFilteredQuery(filter)
            .GroupBy(x => new { x.TasinirKartId, x.StokKodu, x.MalzemeAd, x.Birim })
            .Select(g => new SarfTuketimMalzemeOzetDto
            {
                TasinirKartId = g.Key.TasinirKartId,
                StokKodu = g.Key.StokKodu,
                MalzemeAd = g.Key.MalzemeAd,
                Birim = g.Key.Birim,
                ToplamTuketimMiktari = g.Sum(x => x.NetMiktar),
                SarfFisiSayisi = g.Select(x => x.SarfFisiId).Distinct().Count(),
                ToplamTuketimMaliyeti = g.Sum(x => x.NetToplamMaliyet ?? 0m)
            })
            .OrderBy(x => x.StokKodu)
            .ThenBy(x => x.MalzemeAd)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SarfTuketimKullanimYeriOzetDto>> GetKullanimYeriOzetAsync(
        SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        await ValidateAndEnsureScopeAsync(filter, cancellationToken);

        var groupedRows = await BuildFilteredQuery(filter)
            .GroupBy(x => new { x.IsletmeAlaniId, x.IsletmeAlaniAd, x.OdaId, x.OdaAd, x.Birim })
            .Select(g => new
            {
                g.Key.IsletmeAlaniId,
                g.Key.IsletmeAlaniAd,
                g.Key.OdaId,
                g.Key.OdaAd,
                g.Key.Birim,
                ToplamMiktar = g.Sum(x => x.NetMiktar),
                ToplamMaliyet = g.Sum(x => x.NetToplamMaliyet ?? 0m),
                FarkliMalzemeSayisi = g.Select(x => x.TasinirKartId).Distinct().Count(),
                ToplamSarfSatiriSayisi = g.Count()
            })
            .OrderBy(x => x.IsletmeAlaniAd)
            .ThenBy(x => x.OdaAd)
            .ThenBy(x => x.Birim)
            .ToListAsync(cancellationToken);

        return groupedRows
            .GroupBy(x => new { x.IsletmeAlaniId, x.IsletmeAlaniAd, x.OdaId, x.OdaAd })
            .Select(g => new SarfTuketimKullanimYeriOzetDto
            {
                IsletmeAlaniId = g.Key.IsletmeAlaniId,
                IsletmeAlaniAd = g.Key.IsletmeAlaniAd,
                OdaId = g.Key.OdaId,
                OdaAd = g.Key.OdaAd,
                FarkliMalzemeSayisi = g.Sum(x => x.FarkliMalzemeSayisi),
                ToplamSarfSatiriSayisi = g.Sum(x => x.ToplamSarfSatiriSayisi),
                ToplamMiktarOzeti = string.Join(", ", g
                    .Where(x => x.ToplamMiktar != 0)
                    .Select(x => $"{x.ToplamMiktar:N2} {x.Birim}")),
                ToplamTuketimMaliyeti = g.Sum(x => x.ToplamMaliyet)
            })
            .OrderBy(x => x.IsletmeAlaniAd)
            .ThenBy(x => x.OdaAd)
            .ToList();
    }

    private IQueryable<SarfRaporKaydi> BuildFilteredQuery(SarfTuketimRaporFilterDto filter)
    {
        var query = _dbContext.SarfFisiSatirlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.SarfFisi != null
                && !x.SarfFisi.IsDeleted
                && x.SarfFisi.TesisId == filter.TesisId)
            .Select(x => new SarfRaporKaydi
            {
                SarfFisiId = x.SarfFisiId,
                SarfFisiSatirId = x.Id,
                Tarih = x.SarfFisi!.SarfTarihi,
                DepoId = x.SarfFisi.DepoId,
                DepoKod = x.SarfFisi.Depo != null ? x.SarfFisi.Depo.Kod : string.Empty,
                DepoAd = x.SarfFisi.Depo != null ? x.SarfFisi.Depo.Ad : string.Empty,
                IsletmeAlaniId = x.SarfFisi.IsletmeAlaniId,
                IsletmeAlaniAd = x.SarfFisi.IsletmeAlaniAdSnapshot
                    ?? (x.SarfFisi.IsletmeAlani != null
                        ? x.SarfFisi.IsletmeAlani.OzelAd ?? (x.SarfFisi.IsletmeAlani.IsletmeAlaniSinifi != null ? x.SarfFisi.IsletmeAlani.IsletmeAlaniSinifi.Ad : null)
                        : null),
                OdaId = x.SarfFisi.OdaId,
                OdaAd = x.SarfFisi.OdaNoSnapshot != null
                    ? (x.SarfFisi.OdaBinaAdiSnapshot != null && x.SarfFisi.OdaBinaAdiSnapshot != string.Empty
                        ? x.SarfFisi.OdaNoSnapshot + " - " + x.SarfFisi.OdaBinaAdiSnapshot
                        : x.SarfFisi.OdaNoSnapshot)
                    : (x.SarfFisi.Oda != null
                        ? (x.SarfFisi.Oda.Bina != null
                            ? x.SarfFisi.Oda.OdaNo + " - " + x.SarfFisi.Oda.Bina.Ad
                            : x.SarfFisi.Oda.OdaNo)
                        : null),
                SarfNedeni = x.SarfFisi.SarfNedeni,
                Durum = x.SarfFisi.Durum,
                TasinirKartId = x.TasinirKartId,
                StokKodu = x.StokKodu,
                MalzemeAd = x.TasinirKartAd,
                Birim = x.Birim,
                LotNo = x.LotNo,
                SeriNo = x.SeriNo,
                Miktar = x.Miktar,
                NetMiktar = x.SarfFisi.Durum == SarfFisiDurumlari.Kesinlesti ? x.Miktar : 0m,
                MaliyetBirimFiyat = x.SarfFisi.Durum == SarfFisiDurumlari.Kesinlesti && x.StokHareket != null ? x.StokHareket.MaliyetBirimFiyat : null,
                ToplamMaliyet = x.SarfFisi.Durum == SarfFisiDurumlari.Kesinlesti && x.StokHareket != null ? x.StokHareket.MaliyetTutari : null,
                NetToplamMaliyet = x.SarfFisi.Durum == SarfFisiDurumlari.Kesinlesti && x.StokHareket != null ? x.StokHareket.MaliyetTutari : 0m
            });

        if (filter.BaslangicTarihi.HasValue)
        {
            var baslangic = filter.BaslangicTarihi.Value.Date;
            query = query.Where(x => x.Tarih >= baslangic);
        }

        if (filter.BitisTarihi.HasValue)
        {
            var bitisExclusive = filter.BitisTarihi.Value.Date.AddDays(1);
            query = query.Where(x => x.Tarih < bitisExclusive);
        }

        if (filter.DepoId.HasValue && filter.DepoId.Value > 0)
        {
            query = query.Where(x => x.DepoId == filter.DepoId.Value);
        }

        if (filter.TasinirKartId.HasValue && filter.TasinirKartId.Value > 0)
        {
            query = query.Where(x => x.TasinirKartId == filter.TasinirKartId.Value);
        }

        if (filter.IsletmeAlaniId.HasValue && filter.IsletmeAlaniId.Value > 0)
        {
            query = query.Where(x => x.IsletmeAlaniId == filter.IsletmeAlaniId.Value);
        }

        if (filter.OdaId.HasValue && filter.OdaId.Value > 0)
        {
            query = query.Where(x => x.OdaId == filter.OdaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SarfNedeni))
        {
            var sarfNedeni = filter.SarfNedeni.Trim();
            query = query.Where(x => x.SarfNedeni != null && x.SarfNedeni.Contains(sarfNedeni));
        }

        var durum = string.IsNullOrWhiteSpace(filter.Durum) ? SarfFisiDurumlari.Kesinlesti : filter.Durum.Trim();
        query = query.Where(x => x.Durum == durum);

        return query;
    }

    private async Task ValidateAndEnsureScopeAsync(SarfTuketimRaporFilterDto filter, CancellationToken cancellationToken)
    {
        if (filter.TesisId <= 0)
        {
            throw new BaseException("Geçersiz tesis id.", 400);
        }

        if (filter.BaslangicTarihi.HasValue && filter.BitisTarihi.HasValue && filter.BaslangicTarihi.Value.Date > filter.BitisTarihi.Value.Date)
        {
            throw new BaseException("Başlangıç tarihi bitiş tarihinden büyük olamaz.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(filter.TesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }
    }

    private static SarfTuketimDetayRaporSatirDto ToDetayDto(SarfRaporKaydi x)
        => new()
        {
            Tarih = x.Tarih,
            SarfFisiId = x.SarfFisiId,
            SarfFisiSatirId = x.SarfFisiSatirId,
            DepoId = x.DepoId,
            DepoKod = x.DepoKod,
            DepoAd = x.DepoAd,
            IsletmeAlaniId = x.IsletmeAlaniId,
            IsletmeAlaniAd = x.IsletmeAlaniAd,
            OdaId = x.OdaId,
            OdaAd = x.OdaAd,
            SarfNedeni = x.SarfNedeni,
            TasinirKartId = x.TasinirKartId,
            StokKodu = x.StokKodu,
            MalzemeAd = x.MalzemeAd,
            Birim = x.Birim,
            Miktar = x.NetMiktar,
            LotNo = x.LotNo,
            SeriNo = x.SeriNo,
            Durum = x.Durum,
            MaliyetBirimFiyat = x.Durum == SarfFisiDurumlari.Kesinlesti ? x.MaliyetBirimFiyat : 0m,
            ToplamMaliyet = x.Durum == SarfFisiDurumlari.Kesinlesti ? x.ToplamMaliyet : 0m
        };

    private sealed class SarfRaporKaydi
    {
        public int SarfFisiId { get; set; }
        public int SarfFisiSatirId { get; set; }
        public DateTime Tarih { get; set; }
        public int DepoId { get; set; }
        public string DepoKod { get; set; } = string.Empty;
        public string DepoAd { get; set; } = string.Empty;
        public int? IsletmeAlaniId { get; set; }
        public string? IsletmeAlaniAd { get; set; }
        public int? OdaId { get; set; }
        public string? OdaAd { get; set; }
        public string? SarfNedeni { get; set; }
        public string Durum { get; set; } = string.Empty;
        public int TasinirKartId { get; set; }
        public string StokKodu { get; set; } = string.Empty;
        public string MalzemeAd { get; set; } = string.Empty;
        public string Birim { get; set; } = string.Empty;
        public string? LotNo { get; set; }
        public string? SeriNo { get; set; }
        public decimal Miktar { get; set; }
        public decimal NetMiktar { get; set; }
        public decimal? MaliyetBirimFiyat { get; set; }
        public decimal? ToplamMaliyet { get; set; }
        public decimal? NetToplamMaliyet { get; set; }
    }
}

using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Bildirimler;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Licensing;
using STYS.Odalar;
using STYS.OdaTemizlik.Dto;
using TOD.Platform.Licensing.Abstractions;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaTemizlik.Services;

public class OdaTemizlikService : IOdaTemizlikService
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IBildirimService _bildirimService;
    private readonly ILicenseService _licenseService;

    public OdaTemizlikService(
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        IBildirimService bildirimService,
        ILicenseService licenseService)
    {
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
        _bildirimService = bildirimService;
        _licenseService = licenseService;
    }

    public async Task<List<OdaTemizlikTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);

        var query = _stysDbContext.Tesisler
            .Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new OdaTemizlikTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<OdaTemizlikKayitDto>> GetPagedAsync(
        PagedRequest request,
        string? query,
        int? tesisId,
        string? durum,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (tesisId.HasValue && tesisId.Value > 0 && scope.IsScoped && !scope.TesisIds.Contains(tesisId.Value))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }

        var normalizedQuery = query?.Trim();
        var normalizedDurum = NormalizeDurumFilter(durum);
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        var baseQuery =
            from oda in _stysDbContext.Odalar
            join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
            join tesis in _stysDbContext.Tesisler on bina.TesisId equals tesis.Id
            join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
            where oda.AktifMi
                  && bina.AktifMi
                  && tesis.AktifMi
                  && odaTipi.AktifMi
            select new OdaTemizlikQueryRow
            {
                OdaId = oda.Id,
                TesisId = tesis.Id,
                TesisAdi = tesis.Ad,
                BinaId = bina.Id,
                BinaAdi = bina.Ad,
                OdaNo = oda.OdaNo,
                OdaTipiId = odaTipi.Id,
                OdaTipiAdi = odaTipi.Ad,
                TemizlikDurumu = oda.TemizlikDurumu
            };

        if (scope.IsScoped)
        {
            baseQuery = baseQuery.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            baseQuery = baseQuery.Where(x => x.TesisId == tesisId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedDurum))
        {
            baseQuery = baseQuery.Where(x => x.TemizlikDurumu == normalizedDurum);
        }
        else
        {
            baseQuery = baseQuery.Where(x =>
                x.TemizlikDurumu == OdaTemizlikDurumlari.Kirli
                || x.TemizlikDurumu == OdaTemizlikDurumlari.Temizleniyor);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            baseQuery = baseQuery.Where(x =>
                x.OdaNo.Contains(normalizedQuery)
                || x.BinaAdi.Contains(normalizedQuery)
                || x.TesisAdi.Contains(normalizedQuery)
                || x.OdaTipiAdi.Contains(normalizedQuery));
        }

        baseQuery = ApplyOrdering(baseQuery, sortBy, desc);

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;

        var items = await baseQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OdaTemizlikKayitDto
            {
                OdaId = x.OdaId,
                TesisId = x.TesisId,
                TesisAdi = x.TesisAdi,
                BinaId = x.BinaId,
                BinaAdi = x.BinaAdi,
                OdaNo = x.OdaNo,
                OdaTipiId = x.OdaTipiId,
                OdaTipiAdi = x.OdaTipiAdi,
                TemizlikDurumu = x.TemizlikDurumu
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<OdaTemizlikKayitDto>(
            items,
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task<OdaTemizlikKayitDto> BaslatTemizlikAsync(int odaId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.OdaTemizlik, cancellationToken);

        var room = await GetRoomForStatusChangeAsync(odaId, cancellationToken);

        if (room.Oda.TemizlikDurumu != OdaTemizlikDurumlari.Kirli)
        {
            throw new BaseException("Temizlik baslatma islemi yalnizca Kirli durumundaki odalarda yapilabilir.", 400);
        }

        room.Oda.TemizlikDurumu = OdaTemizlikDurumlari.Temizleniyor;
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            room.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "TemizlikBasladi",
                Baslik = "Oda Temizligi Basladi",
                Mesaj = $"{room.BinaAdi} / {room.Oda.OdaNo} odasi icin temizlik islemi baslatildi.",
                Severity = BildirimSeverityleri.Info,
                Link = "/oda-temizlik-yonetimi"
            },
            cancellationToken);

        return ToDto(room);
    }

    public async Task<OdaTemizlikKayitDto> TamamlaTemizlikAsync(int odaId, CancellationToken cancellationToken = default)
    {
        await _licenseService.EnsureModuleLicensedAsync(StysLicensedModules.OdaTemizlik, cancellationToken);

        var room = await GetRoomForStatusChangeAsync(odaId, cancellationToken);

        if (room.Oda.TemizlikDurumu != OdaTemizlikDurumlari.Temizleniyor)
        {
            throw new BaseException("Temizlik tamamlama islemi yalnizca Temizleniyor durumundaki odalarda yapilabilir.", 400);
        }

        room.Oda.TemizlikDurumu = OdaTemizlikDurumlari.Hazir;
        await _stysDbContext.SaveChangesAsync(cancellationToken);

        await _bildirimService.PublishToTesisUsersAsync(
            room.TesisId,
            new BildirimOlusturRequestDto
            {
                Tip = "TemizlikTamamlandi",
                Baslik = "Oda Temizligi Tamamlandi",
                Mesaj = $"{room.BinaAdi} / {room.Oda.OdaNo} odasi check-in icin hazir duruma getirildi.",
                Severity = BildirimSeverityleri.Success,
                Link = "/oda-temizlik-yonetimi"
            },
            cancellationToken);

        return ToDto(room);
    }

    private async Task<OdaTemizlikRoomContext> GetRoomForStatusChangeAsync(int odaId, CancellationToken cancellationToken)
    {
        if (odaId <= 0)
        {
            throw new BaseException("Gecersiz oda id.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var room = await (
                from oda in _stysDbContext.Odalar
                join bina in _stysDbContext.Binalar on oda.BinaId equals bina.Id
                join tesis in _stysDbContext.Tesisler on bina.TesisId equals tesis.Id
                join odaTipi in _stysDbContext.OdaTipleri on oda.TesisOdaTipiId equals odaTipi.Id
                where oda.Id == odaId
                      && oda.AktifMi
                      && bina.AktifMi
                      && tesis.AktifMi
                      && odaTipi.AktifMi
                select new OdaTemizlikRoomContext
                {
                    Oda = oda,
                    TesisId = tesis.Id,
                    TesisAdi = tesis.Ad,
                    BinaId = bina.Id,
                    BinaAdi = bina.Ad,
                    OdaTipiId = odaTipi.Id,
                    OdaTipiAdi = odaTipi.Ad
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            throw new BaseException("Oda bulunamadi.", 404);
        }

        if (scope.IsScoped && !scope.TesisIds.Contains(room.TesisId))
        {
            throw new BaseException("Bu oda uzerinde islem yapma yetkiniz bulunmuyor.", 403);
        }

        return room;
    }

    private static IQueryable<OdaTemizlikQueryRow> ApplyOrdering(
        IQueryable<OdaTemizlikQueryRow> query,
        string? sortBy,
        bool desc)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query.OrderBy(x => x.TesisAdi).ThenBy(x => x.BinaAdi).ThenBy(x => x.OdaNo).ThenBy(x => x.OdaId);
        }

        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "tesisadi" => desc ? query.OrderByDescending(x => x.TesisAdi).ThenByDescending(x => x.OdaId) : query.OrderBy(x => x.TesisAdi).ThenBy(x => x.OdaId),
            "binaadi" => desc ? query.OrderByDescending(x => x.BinaAdi).ThenByDescending(x => x.OdaId) : query.OrderBy(x => x.BinaAdi).ThenBy(x => x.OdaId),
            "odano" => desc ? query.OrderByDescending(x => x.OdaNo).ThenByDescending(x => x.OdaId) : query.OrderBy(x => x.OdaNo).ThenBy(x => x.OdaId),
            "odatipiadi" => desc ? query.OrderByDescending(x => x.OdaTipiAdi).ThenByDescending(x => x.OdaId) : query.OrderBy(x => x.OdaTipiAdi).ThenBy(x => x.OdaId),
            "temizlikdurumu" => desc ? query.OrderByDescending(x => x.TemizlikDurumu).ThenByDescending(x => x.OdaId) : query.OrderBy(x => x.TemizlikDurumu).ThenBy(x => x.OdaId),
            "odaid" => desc ? query.OrderByDescending(x => x.OdaId) : query.OrderBy(x => x.OdaId),
            _ => query.OrderBy(x => x.TesisAdi).ThenBy(x => x.BinaAdi).ThenBy(x => x.OdaNo).ThenBy(x => x.OdaId)
        };
    }

    private static string? NormalizeDurumFilter(string? durum)
    {
        if (string.IsNullOrWhiteSpace(durum))
        {
            return null;
        }

        var value = durum.Trim();
        if (value.Equals(OdaTemizlikDurumlari.Kirli, StringComparison.OrdinalIgnoreCase))
        {
            return OdaTemizlikDurumlari.Kirli;
        }

        if (value.Equals(OdaTemizlikDurumlari.Temizleniyor, StringComparison.OrdinalIgnoreCase))
        {
            return OdaTemizlikDurumlari.Temizleniyor;
        }

        if (value.Equals(OdaTemizlikDurumlari.Hazir, StringComparison.OrdinalIgnoreCase))
        {
            return OdaTemizlikDurumlari.Hazir;
        }

        throw new BaseException("Gecersiz temizlik durumu filtresi.", 400);
    }

    private static OdaTemizlikKayitDto ToDto(OdaTemizlikRoomContext room)
    {
        return new OdaTemizlikKayitDto
        {
            OdaId = room.Oda.Id,
            TesisId = room.TesisId,
            TesisAdi = room.TesisAdi,
            BinaId = room.BinaId,
            BinaAdi = room.BinaAdi,
            OdaNo = room.Oda.OdaNo,
            OdaTipiId = room.OdaTipiId,
            OdaTipiAdi = room.OdaTipiAdi,
            TemizlikDurumu = room.Oda.TemizlikDurumu
        };
    }

    private sealed class OdaTemizlikQueryRow
    {
        public int OdaId { get; set; }
        public int TesisId { get; set; }
        public string TesisAdi { get; set; } = string.Empty;
        public int BinaId { get; set; }
        public string BinaAdi { get; set; } = string.Empty;
        public string OdaNo { get; set; } = string.Empty;
        public int OdaTipiId { get; set; }
        public string OdaTipiAdi { get; set; } = string.Empty;
        public string TemizlikDurumu { get; set; } = string.Empty;
    }

    private sealed class OdaTemizlikRoomContext
    {
        public STYS.Odalar.Entities.Oda Oda { get; set; } = default!;
        public int TesisId { get; set; }
        public string TesisAdi { get; set; } = string.Empty;
        public int BinaId { get; set; }
        public string BinaAdi { get; set; } = string.Empty;
        public int OdaTipiId { get; set; }
        public string OdaTipiAdi { get; set; } = string.Empty;
    }
}

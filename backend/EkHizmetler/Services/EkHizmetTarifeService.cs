using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;
using STYS.EkHizmetler.Repositories;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.EkHizmetler.Services;

public class EkHizmetTarifeService : BaseRdbmsService<EkHizmetTarifeDto, EkHizmetTarife, int>, IEkHizmetTarifeService
{
    private readonly IEkHizmetTarifeRepository _ekHizmetTarifeRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public EkHizmetTarifeService(
        IEkHizmetTarifeRepository ekHizmetTarifeRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(ekHizmetTarifeRepository, mapper)
    {
        _ekHizmetTarifeRepository = ekHizmetTarifeRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public async Task<List<EkHizmetTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler.Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new EkHizmetTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EkHizmetTarifeDto>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _ekHizmetTarifeRepository.Where(x => x.TesisId == tesisId)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<EkHizmetTarifeDto>>(items);
    }

    public async Task<List<EkHizmetTarifeDto>> UpsertByTesisAsync(int tesisId, IEnumerable<EkHizmetTarifeDto> tarifeler, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        var items = (tarifeler ?? []).ToList();

        foreach (var item in items)
        {
            item.TesisId = tesisId;
            Normalize(item);
        }

        ValidateRows(items);

        var existing = await _ekHizmetTarifeRepository.Where(x => x.TesisId == tesisId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            _ekHizmetTarifeRepository.DeleteRange(existing);
            await _ekHizmetTarifeRepository.SaveChangesAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            var entity = Mapper.Map<EkHizmetTarife>(item);
            entity.Id = 0;
            await _ekHizmetTarifeRepository.AddAsync(entity);
        }

        await _ekHizmetTarifeRepository.SaveChangesAsync(cancellationToken);
        return await GetByTesisIdAsync(tesisId, cancellationToken);
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis secimi.", 400);
        }

        var tesisExists = await _stysDbContext.Tesisler.AnyAsync(x => x.Id == tesisId && x.AktifMi, cancellationToken);
        if (!tesisExists)
        {
            throw new BaseException("Tesis bulunamadi.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private static void Normalize(EkHizmetTarifeDto item)
    {
        item.Ad = item.Ad?.Trim() ?? string.Empty;
        item.Aciklama = string.IsNullOrWhiteSpace(item.Aciklama) ? null : item.Aciklama.Trim();
        item.BirimAdi = string.IsNullOrWhiteSpace(item.BirimAdi) ? "Adet" : item.BirimAdi.Trim();
        item.ParaBirimi = string.IsNullOrWhiteSpace(item.ParaBirimi) ? "TRY" : item.ParaBirimi.Trim().ToUpperInvariant();
        item.BaslangicTarihi = item.BaslangicTarihi.Date;
        item.BitisTarihi = item.BitisTarihi.Date;
    }

    private static void ValidateRows(IReadOnlyCollection<EkHizmetTarifeDto> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Ad))
            {
                throw new BaseException("Ek hizmet adi zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(item.BirimAdi))
            {
                throw new BaseException("Birim adi zorunludur.", 400);
            }

            if (item.BirimFiyat < 0)
            {
                throw new BaseException("Birim fiyat negatif olamaz.", 400);
            }

            if (item.BaslangicTarihi > item.BitisTarihi)
            {
                throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
            }

            if (item.ParaBirimi.Length is < 3 or > 3)
            {
                throw new BaseException("Para birimi 3 karakter olmalidir.", 400);
            }
        }

        var groups = items
            .GroupBy(x => x.Ad.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(x => x.BaslangicTarihi)
                .ThenBy(x => x.BitisTarihi)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].BaslangicTarihi <= ordered[i - 1].BitisTarihi)
                {
                    throw new BaseException($"'{group.Key}' hizmeti icin cakisan tarih araligi tanimlanamaz.", 400);
                }
            }
        }
    }
}

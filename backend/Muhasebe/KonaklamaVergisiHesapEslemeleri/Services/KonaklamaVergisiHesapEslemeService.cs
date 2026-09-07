using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Dtos;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Services;

public class KonaklamaVergisiHesapEslemeService : IKonaklamaVergisiHesapEslemeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IMapper _mapper;

    public KonaklamaVergisiHesapEslemeService(
        StysAppDbContext dbContext,
        IMuhasebeTesisScopeService tesisScopeService,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
        _userAccessScopeService = userAccessScopeService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<KonaklamaVergisiHesapEslemeDto>> GetAllAsync(
        int? tesisId = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var items = await BuildQuery(scope, tesisId, aktifMi)
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.VergiHesap!.TamKod)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<KonaklamaVergisiHesapEslemeDto>>(items);
    }

    public async Task<PagedResult<KonaklamaVergisiHesapEslemeDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildQuery(scope, tesisId, aktifMi);
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.VergiHesap!.TamKod)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KonaklamaVergisiHesapEslemeDto>(
            _mapper.Map<List<KonaklamaVergisiHesapEslemeDto>>(items),
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task<KonaklamaVergisiHesapEslemeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var entity = await BuildQuery(scope, tesisId: null, aktifMi: null)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : _mapper.Map<KonaklamaVergisiHesapEslemeDto>(entity);
    }

    public async Task<KonaklamaVergisiHesapEslemeDto?> GetAktifEslemeAsync(
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && tesisId.HasValue && !scope.TesisIds.Contains(tesisId.Value))
        {
            return null;
        }

        var entity = await ResolveAktifEntityAsync(tesisId, cancellationToken);
        return entity is null ? null : _mapper.Map<KonaklamaVergisiHesapEslemeDto>(entity);
    }

    public async Task<KonaklamaVergisiHesapEslemeDto> AddAsync(
        KonaklamaVergisiHesapEslemeDto dto,
        CancellationToken cancellationToken = default)
    {
        await NormalizeAndValidateAsync(dto, existingId: null, cancellationToken);
        var entity = _mapper.Map<KonaklamaVergisiHesapEsleme>(dto);
        _dbContext.KonaklamaVergisiHesapEslemeleri.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<KonaklamaVergisiHesapEslemeDto> UpdateAsync(
        KonaklamaVergisiHesapEslemeDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Konaklama vergisi hesap eşleme id zorunludur.", 400);
        }

        var entity = await _dbContext.KonaklamaVergisiHesapEslemeleri
            .FirstOrDefaultAsync(x => x.Id == dto.Id.Value && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Konaklama vergisi hesap eşleme bulunamadı.", 404);

        await NormalizeAndValidateAsync(dto, dto.Id.Value, cancellationToken);
        entity.TesisId = dto.TesisId;
        entity.VergiHesapId = dto.VergiHesapId;
        entity.AktifMi = dto.AktifMi;
        entity.Aciklama = dto.Aciklama;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.KonaklamaVergisiHesapEslemeleri
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Konaklama vergisi hesap eşleme bulunamadı.", 404);

        if (entity.TesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(entity.TesisId.Value, cancellationToken);
        }

        entity.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> ResolveAktifHesapIdAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        var entity = await ResolveAktifEntityAsync(tesisId, cancellationToken);
        return entity?.VergiHesapId;
    }

    public async Task<List<KonaklamaVergisiHesapSecenekDto>> GetHesapSecenekleriAsync(
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        if (tesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
        }

        var query = _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.AktifMi && x.DetayHesapMi && x.HareketGorebilirMi);

        query = tesisId.HasValue
            ? query.Where(x => x.TesisId == null || x.TesisId == tesisId.Value)
            : query.Where(x => x.TesisId == null);

        return await query
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Ad)
            .Take(300)
            .Select(x => new KonaklamaVergisiHesapSecenekDto
            {
                Id = x.Id,
                Kod = x.TamKod,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<KonaklamaVergisiHesapEsleme> BuildQuery(
        DomainAccessScope scope,
        int? tesisId,
        bool? aktifMi)
    {
        var query = _dbContext.KonaklamaVergisiHesapEslemeleri
            .AsNoTracking()
            .Include(x => x.Tesis)
            .Include(x => x.VergiHesap)
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => x.TesisId == null || (x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value)));
        }

        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        if (aktifMi.HasValue)
        {
            query = query.Where(x => x.AktifMi == aktifMi.Value);
        }

        return query;
    }

    private async Task<KonaklamaVergisiHesapEsleme?> ResolveAktifEntityAsync(
        int? tesisId,
        CancellationToken cancellationToken)
    {
        if (tesisId.HasValue)
        {
            var tesisOzel = await _dbContext.KonaklamaVergisiHesapEslemeleri
                .AsNoTracking()
                .Include(x => x.VergiHesap)
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.AktifMi && x.TesisId == tesisId.Value, cancellationToken);

            if (tesisOzel is not null && HesapGecerliMi(tesisOzel.VergiHesap, tesisId.Value))
            {
                return tesisOzel;
            }
        }

        var global = await _dbContext.KonaklamaVergisiHesapEslemeleri
            .AsNoTracking()
            .Include(x => x.VergiHesap)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.AktifMi && x.TesisId == null, cancellationToken);

        if (global is not null && (!tesisId.HasValue || HesapGecerliMi(global.VergiHesap, tesisId.Value)))
        {
            return global;
        }

        return null;
    }

    private async Task NormalizeAndValidateAsync(
        KonaklamaVergisiHesapEslemeDto dto,
        int? existingId,
        CancellationToken cancellationToken)
    {
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (dto.TesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(dto.TesisId.Value, cancellationToken);
        }

        if (dto.VergiHesapId <= 0)
        {
            throw new BaseException("Konaklama vergisi hesabı zorunludur.", 400);
        }

        if (dto.AktifMi)
        {
            var duplicateQuery = _dbContext.KonaklamaVergisiHesapEslemeleri
                .Where(x => !x.IsDeleted && x.AktifMi);

            duplicateQuery = dto.TesisId.HasValue
                ? duplicateQuery.Where(x => x.TesisId == dto.TesisId.Value)
                : duplicateQuery.Where(x => x.TesisId == null);

            if (existingId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(x => x.Id != existingId.Value);
            }

            if (await duplicateQuery.AnyAsync(cancellationToken))
            {
                throw new BaseException("Bu tesis için aktif konaklama vergisi hesap eşlemesi zaten mevcut.", 400);
            }
        }

        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.VergiHesapId, cancellationToken)
            ?? throw new BaseException("Seçilen konaklama vergisi hesabı bulunamadı.", 400);

        if (!HesapGecerliMi(hesap, dto.TesisId))
        {
            throw new BaseException("Konaklama vergisi hesabı aktif, detay, hareket görebilir ve eşleme tesisiyle uyumlu olmalıdır.", 400);
        }
    }

    private static bool HesapGecerliMi(STYS.Muhasebe.MuhasebeHesapPlanlari.Entities.MuhasebeHesapPlani? hesap, int? tesisId)
    {
        if (hesap is null || hesap.IsDeleted || !hesap.AktifMi || !hesap.DetayHesapMi || !hesap.HareketGorebilirMi)
        {
            return false;
        }

        if (!tesisId.HasValue)
        {
            return !hesap.TesisId.HasValue;
        }

        return !hesap.TesisId.HasValue || hesap.TesisId == tesisId.Value;
    }
}

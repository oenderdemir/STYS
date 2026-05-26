using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Services;

public class TevkifatHesapEslemeService
    : BaseRdbmsService<TevkifatHesapEslemeDto, TevkifatHesapEsleme, int>,
      ITevkifatHesapEslemeService
{
    private readonly ITevkifatHesapEslemeRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public TevkifatHesapEslemeService(
        ITevkifatHesapEslemeRepository repository,
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<IEnumerable<TevkifatHesapEslemeDto>> GetAllAsync(
        int? tesisId = null,
        string? islemYonu = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildQuery(scope, tesisId, islemYonu, aktifMi);
        var items = await query
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.IslemYonu)
            .ThenBy(x => x.TevkifatPay)
            .ThenBy(x => x.TevkifatPayda)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<TevkifatHesapEslemeDto>>(items);
    }

    public async Task<PagedResult<TevkifatHesapEslemeDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId = null,
        string? islemYonu = null,
        bool? aktifMi = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildQuery(scope, tesisId, islemYonu, aktifMi);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var items = await query
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.IslemYonu)
            .ThenBy(x => x.TevkifatPay)
            .ThenBy(x => x.TevkifatPayda)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TevkifatHesapEslemeDto>(
            Mapper.Map<List<TevkifatHesapEslemeDto>>(items),
            pageNumber,
            pageSize,
            totalCount);
    }

    public override async Task<TevkifatHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<TevkifatHesapEsleme>, IQueryable<TevkifatHesapEsleme>>? include = null)
    {
        var entity = await _dbContext.TevkifatHesapEslemeleri
            .Include(x => x.Tesis)
            .Include(x => x.MuhasebeHesapPlani)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (entity is null)
        {
            return null;
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && entity.TesisId.HasValue && !scope.TesisIds.Contains(entity.TesisId.Value))
        {
            return null;
        }

        return Mapper.Map<TevkifatHesapEslemeDto>(entity);
    }

    public async Task<TevkifatHesapEslemeDto?> GetAktifEslemeAsync(
        int? tesisId,
        string islemYonu,
        int tevkifatPay,
        int tevkifatPayda,
        CancellationToken cancellationToken = default)
    {
        var normalizedIslemYonu = NormalizeIslemYonu(islemYonu);

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && tesisId.HasValue && !scope.TesisIds.Contains(tesisId.Value))
        {
            return null;
        }

        var entity = await _repository.GetAktifEslemeAsync(
            tesisId,
            normalizedIslemYonu,
            tevkifatPay,
            tevkifatPayda,
            cancellationToken);

        return entity is null ? null : Mapper.Map<TevkifatHesapEslemeDto>(entity);
    }

    public override async Task<TevkifatHesapEslemeDto> AddAsync(TevkifatHesapEslemeDto dto)
    {
        await NormalizeAndValidateAsync(dto, null, CancellationToken.None);
        return await base.AddAsync(dto);
    }

    public override async Task<TevkifatHesapEslemeDto> UpdateAsync(TevkifatHesapEslemeDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tevkifat hesap eşleme id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value, CancellationToken.None);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _dbContext.TevkifatHesapEslemeleri
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (entity is null)
        {
            throw new BaseException("Tevkifat hesap eşleme bulunamadı.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && entity.TesisId.HasValue && !scope.TesisIds.Contains(entity.TesisId.Value))
        {
            throw new BaseException("Seçilen kayıt için yetkiniz bulunmamaktadır.", 403);
        }

        await base.DeleteAsync(id);
    }

    private IQueryable<TevkifatHesapEsleme> BuildQuery(
        DomainAccessScope scope,
        int? tesisId,
        string? islemYonu,
        bool? aktifMi)
    {
        var query = _dbContext.TevkifatHesapEslemeleri
            .Include(x => x.Tesis)
            .Include(x => x.MuhasebeHesapPlani)
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => x.TesisId == null || (x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value)));
        }

        if (tesisId.HasValue)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        if (!string.IsNullOrWhiteSpace(islemYonu))
        {
            var normalized = NormalizeIslemYonu(islemYonu);
            query = query.Where(x => x.IslemYonu == normalized);
        }

        if (aktifMi.HasValue)
        {
            query = query.Where(x => x.AktifMi == aktifMi.Value);
        }

        return query;
    }

    private async Task NormalizeAndValidateAsync(TevkifatHesapEslemeDto dto, int? existingId, CancellationToken cancellationToken)
    {
        dto.IslemYonu = NormalizeIslemYonu(dto.IslemYonu);
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (dto.TevkifatPay <= 0)
            throw new BaseException("Tevkifat payı 0'dan büyük olmalıdır.", 400);

        if (dto.TevkifatPayda <= 0)
            throw new BaseException("Tevkifat paydası 0'dan büyük olmalıdır.", 400);

        if (dto.TevkifatPay > dto.TevkifatPayda)
            throw new BaseException("Tevkifat payı paydadan büyük olamaz.", 400);

        if (dto.AktifMi)
        {
            var duplicateQuery = _dbContext.TevkifatHesapEslemeleri
                .Where(x =>
                    !x.IsDeleted &&
                    x.AktifMi &&
                    x.IslemYonu == dto.IslemYonu &&
                    x.TevkifatPay == dto.TevkifatPay &&
                    x.TevkifatPayda == dto.TevkifatPayda);

            duplicateQuery = dto.TesisId.HasValue
                ? duplicateQuery.Where(x => x.TesisId == dto.TesisId.Value)
                : duplicateQuery.Where(x => x.TesisId == null);

            if (existingId.HasValue)
            {
                duplicateQuery = duplicateQuery.Where(x => x.Id != existingId.Value);
            }

            if (await duplicateQuery.AnyAsync(cancellationToken))
            {
                throw new BaseException("Aynı tesis, işlem yönü ve oran için aktif tevkifat hesap eşlemesi zaten mevcut.", 400);
            }
        }

        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.MuhasebeHesapPlaniId, cancellationToken);

        if (hesap is null)
            throw new BaseException("Seçilen muhasebe hesabı bulunamadı.", 400);

        if (hesap.IsDeleted)
            throw new BaseException("Seçilen muhasebe hesabı silinmiştir.", 400);

        if (!hesap.AktifMi)
            throw new BaseException("Seçilen muhasebe hesabı aktif değildir.", 400);

        if (!hesap.DetayHesapMi)
            throw new BaseException("Tevkifat hesap eşlemesi için detay hesap seçilmelidir.", 400);

        if (!hesap.HareketGorebilirMi)
            throw new BaseException("Tevkifat hesap eşlemesi için hareket görebilir hesap seçilmelidir.", 400);

        if (!dto.TesisId.HasValue)
        {
            if (hesap.TesisId.HasValue)
            {
                throw new BaseException("Global tevkifat eşlemesi için global hesap seçilmelidir.", 400);
            }
        }
        else if (hesap.TesisId.HasValue && hesap.TesisId.Value != dto.TesisId.Value)
        {
            throw new BaseException("Seçilen muhasebe hesabı eşleme tesisiyle uyumlu değildir.", 400);
        }

        if (hesap.TesisId.HasValue)
        {
            var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
            if (scope.IsScoped && !scope.TesisIds.Contains(hesap.TesisId.Value))
            {
                throw new BaseException("Seçilen muhasebe hesabı için yetkiniz bulunmamaktadır.", 403);
            }
        }

        if (dto.TesisId.HasValue)
        {
            var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
            if (scope.IsScoped && !scope.TesisIds.Contains(dto.TesisId.Value))
            {
                throw new BaseException("Seçilen tesis için yetkiniz bulunmamaktadır.", 403);
            }
        }
    }

    private static string NormalizeIslemYonu(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(normalized, TevkifatIslemYonleri.Satis, StringComparison.OrdinalIgnoreCase))
            return TevkifatIslemYonleri.Satis;
        if (string.Equals(normalized, TevkifatIslemYonleri.Alis, StringComparison.OrdinalIgnoreCase))
            return TevkifatIslemYonleri.Alis;

        throw new BaseException($"Desteklenmeyen işlem yönü: {value}.", 400);
    }
}

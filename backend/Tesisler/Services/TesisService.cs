using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Iller.Repositories;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Repositories;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tesisler.Services;

public class TesisService : BaseRdbmsService<TesisDto, Tesis, int>, ITesisService
{
    private readonly ITesisRepository _tesisRepository;
    private readonly ITesisYoneticiRepository _tesisYoneticiRepository;
    private readonly IIlRepository _ilRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public TesisService(
        ITesisRepository tesisRepository,
        ITesisYoneticiRepository tesisYoneticiRepository,
        IIlRepository ilRepository,
        IUserRepository userRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(tesisRepository, mapper)
    {
        _tesisRepository = tesisRepository;
        _tesisYoneticiRepository = tesisYoneticiRepository;
        _ilRepository = ilRepository;
        _userRepository = userRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<TesisDto> AddAsync(TesisDto dto)
    {
        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, null);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: false);

        var entity = Mapper.Map<Tesis>(dto);
        entity.Yoneticiler = managerIds!
            .Select(x => new TesisYonetici
            {
                UserId = x
            })
            .ToList();

        await _tesisRepository.AddAsync(entity);
        await _tesisRepository.SaveChangesAsync();

        return Mapper.Map<TesisDto>(entity);
    }

    public override async Task<TesisDto> UpdateAsync(TesisDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tesis id zorunludur.", 400);
        }

        var existingEntity = await _tesisRepository.GetByIdAsync(dto.Id.Value, query => query.Include(x => x.Yoneticiler));
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek tesis bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existingEntity.Id);
        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: true);

        existingEntity.IsDeleted = false;
        existingEntity.Ad = dto.Ad;
        existingEntity.IlId = dto.IlId;
        existingEntity.Telefon = dto.Telefon;
        existingEntity.Adres = dto.Adres;
        existingEntity.Eposta = dto.Eposta;
        existingEntity.AktifMi = dto.AktifMi;

        if (managerIds is not null)
        {
            SyncYoneticiler(existingEntity, managerIds);
        }

        _tesisRepository.Update(existingEntity);
        await _tesisRepository.SaveChangesAsync();

        return Mapper.Map<TesisDto>(existingEntity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanAccessTesisAsync(id);
        await base.DeleteAsync(id);
    }

    public override async Task<TesisDto?> GetByIdAsync(int id, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<TesisDto>> GetAllAsync(Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<TesisDto>> WhereAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<TesisDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<Tesis, bool>>? predicate = null,
        Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null,
        Func<IQueryable<Tesis>, IOrderedQueryable<Tesis>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureIlRulesAsync(int ilId)
    {
        var il = await _ilRepository.GetByIdAsync(ilId);
        if (il is null)
        {
            throw new BaseException("Secilen il bulunamadi.", 400);
        }

        if (!il.AktifMi)
        {
            throw new BaseException("Pasif il altinda tesis olusturulamaz veya guncellenemez.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.IlIds.Contains(ilId))
        {
            throw new BaseException("Bu il altinda tesis yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(TesisDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _tesisRepository.AnyAsync(x =>
            x.AktifMi &&
            x.IlId == dto.IlId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni il altinda ayni isimde aktif tesis zaten mevcut.", 400);
        }
    }

    private static void Normalize(TesisDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Tesis adi zorunludur.", 400);
        }

        if (dto.IlId <= 0)
        {
            throw new BaseException("Il secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Telefon))
        {
            throw new BaseException("Telefon zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Adres))
        {
            throw new BaseException("Adres zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
        dto.Telefon = dto.Telefon.Trim();
        dto.Adres = dto.Adres.Trim();
        dto.Eposta = string.IsNullOrWhiteSpace(dto.Eposta) ? null : dto.Eposta.Trim();
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis kaydi icin yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<Tesis>, IQueryable<Tesis>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Tesis>, IQueryable<Tesis>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            result = result.Include(x => x.Yoneticiler);

            if (scope.IsScoped)
            {
                result = result.Where(x => scope.TesisIds.Contains(x.Id));
            }

            return result;
        };
    }

    private async Task<List<Guid>?> NormalizeAndValidateManagerIdsAsync(
        ICollection<Guid>? managerUserIds,
        bool preserveWhenNull)
    {
        if (managerUserIds is null)
        {
            return preserveWhenNull ? null : [];
        }

        var normalizedManagerIds = managerUserIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedManagerIds.Count == 0)
        {
            return [];
        }

        var existingUserIds = await _userRepository
            .Where(x => normalizedManagerIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        var missingUserIds = normalizedManagerIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
        {
            throw new BaseException("Secilen yoneticilerden en az biri bulunamadi.", 400);
        }

        return normalizedManagerIds;
    }

    private void SyncYoneticiler(Tesis entity, IReadOnlyCollection<Guid> managerUserIds)
    {
        entity.Yoneticiler ??= [];

        var byUserId = entity.Yoneticiler.ToDictionary(x => x.UserId);
        var desiredUserIds = managerUserIds.ToHashSet();

        var toDelete = entity.Yoneticiler
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _tesisYoneticiRepository.DeleteRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Yoneticiler.Add(new TesisYonetici
            {
                UserId = desiredUserId
            });
        }
    }
}

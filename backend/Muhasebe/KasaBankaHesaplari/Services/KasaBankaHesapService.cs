using AutoMapper;
using System;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KasaBankaHesaplari.Services;

public class KasaBankaHesapService : BaseRdbmsService<KasaBankaHesapDto, KasaBankaHesap, int>, IKasaBankaHesapService
{
    private readonly IKasaBankaHesapRepository _repository;
    private readonly IMuhasebeHesapPlaniRepository _muhasebeHesapPlaniRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _dbContext;

    public KasaBankaHesapService(IKasaBankaHesapRepository repository, IMuhasebeHesapPlaniRepository muhasebeHesapPlaniRepository, IUserAccessScopeService userAccessScopeService, StysAppDbContext dbContext, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _muhasebeHesapPlaniRepository = muhasebeHesapPlaniRepository;
        _userAccessScopeService = userAccessScopeService;
        _dbContext = dbContext;
    }

    public override async Task<KasaBankaHesapDto> AddAsync(KasaBankaHesapDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<KasaBankaHesapDto> UpdateAsync(KasaBankaHesapDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap id zorunludur.", 400);
        }

        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id.Value);
        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<KasaBankaHesapDto>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var items = await _repository.GetByTipAsync(tip, onlyActive, cancellationToken);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            items = items.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value)).ToList();
        }
        return items.Select(Mapper.Map<KasaBankaHesapDto>).ToList();
    }

    public override async Task<KasaBankaHesapDto?> GetByIdAsync(int id, Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<KasaBankaHesapDto>> GetAllAsync(Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<KasaBankaHesapDto>> WhereAsync(System.Linq.Expressions.Expression<Func<KasaBankaHesap, bool>> predicate, Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<KasaBankaHesapDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<KasaBankaHesap, bool>>? predicate = null,
        Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include = null,
        Func<IQueryable<KasaBankaHesap>, IOrderedQueryable<KasaBankaHesap>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    public async Task<List<MuhasebeHesapSecimDto>> GetMuhasebeHesapSecimleriAsync(string tip, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var prefix = tip == KasaBankaHesapTipleri.NakitKasa ? "1.10.100" : "1.10.102";
        var matches = await _muhasebeHesapPlaniRepository.GetByTamKodPrefixAsync(prefix, cancellationToken);

        return matches.Select(x => new MuhasebeHesapSecimDto
        {
            Id = x.Id,
            TamKod = x.TamKod,
            Ad = x.Ad
        }).ToList();
    }

    private async Task NormalizeAndValidateAsync(KasaBankaHesapDto dto, int? currentId)
    {
        dto.Tip = (dto.Tip ?? string.Empty).Trim();
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.BankaAdi = NormalizeOptional(dto.BankaAdi, 128);
        dto.SubeAdi = NormalizeOptional(dto.SubeAdi, 128);
        dto.HesapNo = NormalizeOptional(dto.HesapNo, 64);
        dto.Iban = NormalizeOptional(dto.Iban?.Replace(" ", string.Empty).ToUpperInvariant(), 34);
        dto.MusteriNo = NormalizeOptional(dto.MusteriNo, 64);
        dto.HesapTuru = NormalizeOptional(dto.HesapTuru, 32);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        if (!KasaBankaHesapTipleri.TumTipler.Contains(dto.Tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        var duplicateKod = await _repository.AnyAsync(x => x.Kod == dto.Kod && x.TesisId == dto.TesisId && (!currentId.HasValue || x.Id != currentId.Value));
        if (duplicateKod)
        {
            throw new BaseException("Hesap kodu ayni tesis altinda benzersiz olmalidir.", 400);
        }

        var muhasebeHesap = await _muhasebeHesapPlaniRepository.GetByIdAsync(dto.MuhasebeHesapPlaniId);
        if (muhasebeHesap is null)
        {
            throw new BaseException("Muhasebe hesap plani kaydi bulunamadi.", 400);
        }

        if (!muhasebeHesap.AktifMi)
        {
            throw new BaseException("Secilen muhasebe hesabi pasif.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.NakitKasa && !muhasebeHesap.TamKod.StartsWith("1.10.100", StringComparison.Ordinal))
        {
            throw new BaseException("Nakit kasa hesaplari sadece 1.10.100 ile baslayan muhasebe kodlarina baglanabilir.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.Banka && !muhasebeHesap.TamKod.StartsWith("1.10.102", StringComparison.Ordinal))
        {
            throw new BaseException("Banka hesaplari sadece 1.10.102 ile baslayan muhasebe kodlarina baglanabilir.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.Banka)
        {
            if (string.IsNullOrWhiteSpace(dto.BankaAdi))
            {
                throw new BaseException("Banka tipi hesap icin banka adi zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.HesapNo) && string.IsNullOrWhiteSpace(dto.Iban))
            {
                throw new BaseException("Banka tipi hesap icin hesap no veya IBAN zorunludur.", 400);
            }
        }
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    private async Task<int?> ResolveWriteTesisIdAsync(int? tesisId, int? existingId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var candidateTesisId = tesisId;

        if (!candidateTesisId.HasValue && existingId.HasValue)
        {
            candidateTesisId = await _repository.Where(x => x.Id == existingId.Value).Select(x => x.TesisId).FirstOrDefaultAsync();
        }

        if (scope.IsScoped)
        {
            if (!candidateTesisId.HasValue)
            {
                if (scope.TesisIds.Count == 1)
                {
                    candidateTesisId = scope.TesisIds.First();
                }
                else
                {
                    throw new BaseException("Tesis secimi zorunludur.", 400);
                }
            }

            if (!scope.TesisIds.Contains(candidateTesisId.Value))
            {
                throw new BaseException("Secilen tesis icin yetkiniz bulunmuyor.", 403);
            }
        }

        if (candidateTesisId.HasValue && candidateTesisId.Value > 0)
        {
            var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == candidateTesisId.Value && x.AktifMi);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        return candidateTesisId is > 0 ? candidateTesisId : null;
    }

    private static Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<KasaBankaHesap>, IQueryable<KasaBankaHesap>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }

            return result;
        };
    }
}

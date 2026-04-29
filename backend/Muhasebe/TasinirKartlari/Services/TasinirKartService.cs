using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TasinirKartlari.Services;

public class TasinirKartService : BaseRdbmsService<TasinirKartDto, TasinirKart, int>, ITasinirKartService
{
    private readonly ITasinirKartRepository _repository;
    private readonly ITasinirKodRepository _tasinirKodRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeDetayHesapService _muhasebeDetayHesapService;

    public TasinirKartService(
        ITasinirKartRepository repository,
        ITasinirKodRepository tasinirKodRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext dbContext,
        IMapper mapper,
        IMuhasebeDetayHesapService muhasebeDetayHesapService)
        : base(repository, mapper)
    {
        _repository = repository;
        _tasinirKodRepository = tasinirKodRepository;
        _userAccessScopeService = userAccessScopeService;
        _dbContext = dbContext;
        _muhasebeDetayHesapService = muhasebeDetayHesapService;
    }

    public override async Task<TasinirKartDto> AddAsync(TasinirKartDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        await NormalizeAndValidateAsync(dto, null);
        EnsureTesisRequired(dto.TesisId);

        var anaHesapKodu = ResolveTasinirKartAnaHesapKodu();
        await using var tx = await _dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
            var detay = await _muhasebeDetayHesapService.CreateOrResolveDetayHesapAsync(
                dto.TesisId!.Value,
                anaHesapKodu,
                "TasinirKart",
                dto.Ad,
                null,
                CancellationToken.None);

            dto.MuhasebeHesapPlaniId = detay.MuhasebeHesapPlaniId;
            dto.AnaMuhasebeHesapKodu = detay.AnaMuhasebeHesapKodu;
            dto.MuhasebeHesapSiraNo = detay.SiraNo;
            dto.StokKodu = detay.Kod;
            var result = await base.AddAsync(dto);
            await tx.CommitAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task<TasinirKartDto> UpdateAsync(TasinirKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tasinir kart id zorunludur.", 400);
        }

        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id);
        await NormalizeAndValidateAsync(dto, dto.Id);
        EnsureTesisRequired(dto.TesisId);

        var current = await _repository.GetByIdAsync(dto.Id.Value) ?? throw new BaseException("Tasinir kart bulunamadi.", 404);
        if (current.MuhasebeHesapPlaniId.HasValue && current.TesisId != dto.TesisId)
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş taşınır kartlarda tesis değiştirilemez.", 400);
        }

        dto.MuhasebeHesapPlaniId = current.MuhasebeHesapPlaniId;
        dto.AnaMuhasebeHesapKodu = current.AnaMuhasebeHesapKodu;
        dto.MuhasebeHesapSiraNo = current.MuhasebeHesapSiraNo;
        dto.StokKodu = current.StokKodu;

        var result = await base.UpdateAsync(dto);
        if (current.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == current.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.Ad = dto.Ad;
                hesap.TesisId = dto.TesisId;
                if (!dto.AktifMi)
                {
                    hesap.AktifMi = false;
                }

                await _dbContext.SaveChangesAsync();
            }
        }

        return result;
    }

    public override async Task<TasinirKartDto?> GetByIdAsync(int id, Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<TasinirKartDto>> GetAllAsync(Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<TasinirKartDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TasinirKart, bool>> predicate, Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<TasinirKartDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<TasinirKart, bool>>? predicate = null,
        Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null,
        Func<IQueryable<TasinirKart>, IOrderedQueryable<TasinirKart>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task NormalizeAndValidateAsync(TasinirKartDto dto, int? currentId)
    {
        dto.StokKodu = dto.StokKodu?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Birim = dto.Birim?.Trim() ?? "Adet";
        dto.MalzemeTipi = dto.MalzemeTipi?.Trim() ?? string.Empty;
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (dto.TasinirKodId <= 0 || !await _tasinirKodRepository.AnyAsync(x => x.Id == dto.TasinirKodId))
        {
            throw new BaseException("Gecerli bir tasinir kod secilmelidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (!MalzemeTipleri.Hepsi.Contains(dto.MalzemeTipi))
        {
            throw new BaseException("Malzeme tipi gecersiz.", 400);
        }

        if (dto.KdvOrani < 0 || dto.KdvOrani > 100)
        {
            throw new BaseException("KDV orani 0 ile 100 arasinda olmalidir.", 400);
        }

        if (!string.IsNullOrWhiteSpace(dto.StokKodu))
        {
            var duplicate = await _repository.AnyAsync(x => x.StokKodu == dto.StokKodu && x.TesisId == dto.TesisId && (!currentId.HasValue || x.Id != currentId.Value));
            if (duplicate)
            {
                throw new BaseException("Stok kodu ayni tesis altinda benzersiz olmalidir.", 400);
            }
        }
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

    private static Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include)
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

    private static void EnsureTesisRequired(int? tesisId)
    {
        if (!tesisId.HasValue || tesisId.Value <= 0)
        {
            throw new BaseException("Tasinir kart icin tesis secimi zorunludur.", 400);
        }
    }

    private string ResolveTasinirKartAnaHesapKodu()
    {
        return MuhasebeAnaHesapKodlari.TasinirKart;
    }
}

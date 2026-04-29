using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Depolar.Services;

public class DepoService : BaseRdbmsService<DepoDto, Depo, int>, IDepoService
{
    private readonly IDepoRepository _repository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IMuhasebeDetayHesapService _muhasebeDetayHesapService;

    public DepoService(
        IDepoRepository repository,
        ITesisRepository tesisRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext dbContext,
        IMapper mapper,
        IMuhasebeDetayHesapService muhasebeDetayHesapService)
        : base(repository, mapper)
    {
        _repository = repository;
        _tesisRepository = tesisRepository;
        _userAccessScopeService = userAccessScopeService;
        _dbContext = dbContext;
        _mapper = mapper;
        _muhasebeDetayHesapService = muhasebeDetayHesapService;
    }

    public override async Task<DepoDto> AddAsync(DepoDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        await NormalizeAndValidateAsync(dto, null);
        EnsureTesisRequired(dto.TesisId);

        var anaHesapKodu = ResolveDepoAnaHesapKodu();
        await using var tx = await _dbContext.Database.BeginTransactionAsync(CancellationToken.None);
        try
        {
            var detay = await _muhasebeDetayHesapService.CreateOrResolveDetayHesapAsync(
                dto.TesisId!.Value,
                anaHesapKodu,
                "Depo",
                dto.Ad,
                null,
                CancellationToken.None);

            dto.MuhasebeHesapPlaniId = detay.MuhasebeHesapPlaniId;
            dto.AnaMuhasebeHesapKodu = detay.AnaMuhasebeHesapKodu;
            dto.MuhasebeHesapSiraNo = detay.SiraNo;
            dto.Kod = detay.Kod;

            var entity = _mapper.Map<Depo>(dto);
            entity.DepoCikisGruplari = BuildCikisGruplari(dto.CikisGruplari, null);

            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();
            await tx.CommitAsync(CancellationToken.None);
            return await MapDetailDtoAsync(entity.Id);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task<DepoDto> UpdateAsync(DepoDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Depo id zorunludur.", 400);
        }

        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id.Value);
        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        EnsureTesisRequired(dto.TesisId);

        var entity = await _dbContext.Depolar
            .Include(x => x.DepoCikisGruplari)
            .FirstOrDefaultAsync(x => x.Id == dto.Id.Value);

        if (entity is null)
        {
            throw new BaseException("Depo kaydi bulunamadi.", 404);
        }

        var hasMuhasebeLink = entity.MuhasebeHesapPlaniId.HasValue;
        if (hasMuhasebeLink && entity.TesisId != dto.TesisId)
        {
            throw new BaseException("Muhasebe hesabı oluşturulmuş depolarda tesis değiştirilemez.", 400);
        }

        dto.Kod = entity.Kod;
        entity.TesisId = dto.TesisId;
        entity.UstDepoId = dto.UstDepoId;
        entity.Ad = dto.Ad;
        entity.MalzemeKayitTipi = Enum.Parse<DepoMalzemeKayitTipleri>(dto.MalzemeKayitTipi);
        entity.SatisFiyatlariniGoster = dto.SatisFiyatlariniGoster;
        entity.AvansGenel = dto.AvansGenel;
        entity.AktifMi = dto.AktifMi;
        entity.Aciklama = dto.Aciklama;

        SyncCikisGruplari(entity, dto.CikisGruplari);

        if (entity.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == entity.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.Ad = entity.Ad;
                hesap.TesisId = entity.TesisId;
                if (!entity.AktifMi)
                {
                    hesap.AktifMi = false;
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        return await MapDetailDtoAsync(entity.Id);
    }

    public override async Task<DepoDto?> GetByIdAsync(int id, Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        var dto = await base.GetByIdAsync(id, includeQuery);
        if (dto is null)
        {
            return null;
        }

        dto.CikisGruplari = await _dbContext.Set<DepoCikisGrup>()
            .Where(x => x.DepoId == id)
            .OrderBy(x => x.CikisGrupAdi)
            .Select(x => _mapper.Map<DepoCikisGrupDto>(x))
            .ToListAsync();
        return dto;
    }

    public override async Task<IEnumerable<DepoDto>> GetAllAsync(Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<DepoDto>> WhereAsync(System.Linq.Expressions.Expression<Func<Depo, bool>> predicate, Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<DepoDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<Depo, bool>>? predicate = null,
        Func<IQueryable<Depo>, IQueryable<Depo>>? include = null,
        Func<IQueryable<Depo>, IOrderedQueryable<Depo>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    public override async Task DeleteAsync(int id)
    {
        var depo = await _dbContext.Depolar.FirstOrDefaultAsync(x => x.Id == id);
        if (depo is null)
        {
            throw new BaseException("Depo bulunamadi.", 404);
        }

        var hasChildren = await _dbContext.Depolar.AnyAsync(x => x.UstDepoId == id);
        if (hasChildren)
        {
            throw new BaseException("Alt depolari bulunan depo silinemez.", 400);
        }

        await base.DeleteAsync(id);

        if (depo.MuhasebeHesapPlaniId.HasValue)
        {
            var hesap = await _dbContext.MuhasebeHesapPlanlari.FirstOrDefaultAsync(x => x.Id == depo.MuhasebeHesapPlaniId.Value);
            if (hesap is not null)
            {
                hesap.AktifMi = false;
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task NormalizeAndValidateAsync(DepoDto dto, int? currentId)
    {
        dto.Kod = dto.Kod?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();
        dto.MalzemeKayitTipi = NormalizeMalzemeKayitTipi(dto.MalzemeKayitTipi);
        dto.CikisGruplari = dto.CikisGruplari ?? [];

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Depo adi zorunludur.", 400);
        }

        if (dto.TesisId.HasValue && dto.TesisId.Value > 0)
        {
            var tesisExists = await _tesisRepository.AnyAsync(x => x.Id == dto.TesisId.Value);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Kod))
        {
            var duplicate = await _repository.AnyAsync(x => x.Kod == dto.Kod && x.TesisId == dto.TesisId && (!currentId.HasValue || x.Id != currentId.Value));
            if (duplicate)
            {
                throw new BaseException("Depo kodu ayni tesis altinda benzersiz olmalidir.", 400);
            }
        }

        await ValidateParentAsync(dto, currentId);
        ValidateCikisGruplari(dto.CikisGruplari);
    }

    private async Task ValidateParentAsync(DepoDto dto, int? currentId)
    {
        if (!dto.UstDepoId.HasValue || dto.UstDepoId.Value <= 0)
        {
            dto.UstDepoId = null;
            return;
        }

        if (currentId.HasValue && dto.UstDepoId.Value == currentId.Value)
        {
            throw new BaseException("Bir depo kendisini ust depo olarak secemez.", 400);
        }

        var parent = await _repository.GetByIdAsync(dto.UstDepoId.Value);
        if (parent is null)
        {
            throw new BaseException("Secilen ust depo bulunamadi.", 400);
        }

        if (dto.TesisId != parent.TesisId)
        {
            throw new BaseException("Ust depo ayni tesise ait olmalidir.", 400);
        }

        if (currentId.HasValue && await WouldCreateCycleAsync(currentId.Value, dto.UstDepoId.Value))
        {
            throw new BaseException("Depo hiyerarsisinde dongu olusturulamaz.", 400);
        }
    }

    private async Task<bool> WouldCreateCycleAsync(int depoId, int newParentId)
    {
        var cursorId = (int?)newParentId;
        while (cursorId.HasValue)
        {
            if (cursorId.Value == depoId)
            {
                return true;
            }

            cursorId = await _repository.Where(x => x.Id == cursorId.Value).Select(x => x.UstDepoId).FirstOrDefaultAsync();
        }

        return false;
    }

    private static string NormalizeMalzemeKayitTipi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut.ToString();
        }

        if (Enum.TryParse<DepoMalzemeKayitTipleri>(value.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed.ToString();
        }

        throw new BaseException("Malzeme kayit tipi gecersiz.", 400);
    }

    private static void ValidateCikisGruplari(List<DepoCikisGrupDto> cikisGruplari)
    {
        foreach (var group in cikisGruplari)
        {
            group.CikisGrupAdi = group.CikisGrupAdi?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(group.CikisGrupAdi))
            {
                throw new BaseException("Cikis grup adi zorunludur.", 400);
            }

            if (group.KarOrani < 0)
            {
                throw new BaseException("Kar orani 0'dan kucuk olamaz.", 400);
            }
        }
    }

    private static List<DepoCikisGrup> BuildCikisGruplari(List<DepoCikisGrupDto> groups, int? depoId)
    {
        return groups.Select(x => new DepoCikisGrup
        {
            DepoId = depoId ?? 0,
            CikisGrupAdi = x.CikisGrupAdi,
            KarOrani = x.KarOrani,
            LokasyonId = x.LokasyonId
        }).ToList();
    }

    private void SyncCikisGruplari(Depo entity, List<DepoCikisGrupDto> groups)
    {
        var incoming = groups.Where(x => !string.IsNullOrWhiteSpace(x.CikisGrupAdi)).ToList();
        var incomingIds = incoming.Where(x => x.Id.HasValue && x.Id.Value > 0).Select(x => x.Id!.Value).ToHashSet();

        var toDelete = entity.DepoCikisGruplari.Where(x => !incomingIds.Contains(x.Id)).ToList();
        if (toDelete.Count > 0)
        {
            _dbContext.RemoveRange(toDelete);
        }

        foreach (var item in incoming)
        {
            if (item.Id.HasValue && item.Id.Value > 0)
            {
                var existing = entity.DepoCikisGruplari.FirstOrDefault(x => x.Id == item.Id.Value);
                if (existing is null)
                {
                    continue;
                }

                existing.CikisGrupAdi = item.CikisGrupAdi.Trim();
                existing.KarOrani = item.KarOrani;
                existing.LokasyonId = item.LokasyonId;
            }
            else
            {
                entity.DepoCikisGruplari.Add(new DepoCikisGrup
                {
                    CikisGrupAdi = item.CikisGrupAdi.Trim(),
                    KarOrani = item.KarOrani,
                    LokasyonId = item.LokasyonId
                });
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

        return candidateTesisId is > 0 ? candidateTesisId : null;
    }

    private static void EnsureTesisRequired(int? tesisId)
    {
        if (!tesisId.HasValue || tesisId.Value <= 0)
        {
            throw new BaseException("Depo icin tesis secimi zorunludur.", 400);
        }
    }

    private string ResolveDepoAnaHesapKodu()
    {
        return MuhasebeAnaHesapKodlari.Depo;
    }

    private static Func<IQueryable<Depo>, IQueryable<Depo>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Depo>, IQueryable<Depo>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            result = result.Include(x => x.DepoCikisGruplari);

            if (scope.IsScoped)
            {
                result = result.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }

            return result;
        };
    }

    private async Task<DepoDto> MapDetailDtoAsync(int depoId)
    {
        var entity = await _dbContext.Depolar
            .Include(x => x.DepoCikisGruplari)
            .FirstOrDefaultAsync(x => x.Id == depoId);

        if (entity is null)
        {
            throw new BaseException("Depo bulunamadi.", 404);
        }

        var dto = _mapper.Map<DepoDto>(entity);
        dto.CikisGruplari = entity.DepoCikisGruplari
            .OrderBy(x => x.CikisGrupAdi)
            .Select(x => _mapper.Map<DepoCikisGrupDto>(x))
            .ToList();
        return dto;
    }
}

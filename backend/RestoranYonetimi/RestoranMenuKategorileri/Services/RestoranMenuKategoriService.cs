using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMenuKategorileri.Dtos;
using STYS.RestoranMenuKategorileri.Entities;
using STYS.RestoranMenuKategorileri.Repositories;
using STYS.Restoranlar.Repositories;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranMenuKategorileri.Services;

public class RestoranMenuKategoriService : BaseRdbmsService<RestoranMenuKategoriDto, RestoranMenuKategori, int>, IRestoranMenuKategoriService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranMenuKategoriRepository _kategoriRepository;
    private readonly IRestoranRepository _restoranRepository;
    private readonly IMapper _mapper;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranMenuKategoriService(
        StysAppDbContext dbContext,
        IRestoranMenuKategoriRepository kategoriRepository,
        IRestoranRepository restoranRepository,
        IMapper mapper,
        IRestoranErisimService restoranErisimService)
        : base(kategoriRepository, mapper)
    {
        _dbContext = dbContext;
        _kategoriRepository = kategoriRepository;
        _restoranRepository = restoranRepository;
        _mapper = mapper;
        _restoranErisimService = restoranErisimService;
    }

    public override async Task<IEnumerable<RestoranMenuKategoriDto>> GetAllAsync(Func<IQueryable<RestoranMenuKategori>, IQueryable<RestoranMenuKategori>>? include = null)
    {
        var query = _kategoriRepository.Where(x => true);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.SiraNo).ThenBy(x => x.Ad).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMenuKategoriDto>>(items);
    }

    public override async Task<IEnumerable<RestoranMenuKategoriDto>> WhereAsync(
        Expression<Func<RestoranMenuKategori, bool>> predicate,
        Func<IQueryable<RestoranMenuKategori>, IQueryable<RestoranMenuKategori>>? include = null)
    {
        var query = _kategoriRepository.Where(predicate);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.SiraNo).ThenBy(x => x.Ad).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMenuKategoriDto>>(items);
    }

    public override async Task<RestoranMenuKategoriDto?> GetByIdAsync(int id, Func<IQueryable<RestoranMenuKategori>, IQueryable<RestoranMenuKategori>>? include = null)
    {
        var entity = await _kategoriRepository.GetByIdAsync(id, include);
        if (entity is not null)
        {
            await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        }

        return entity is null ? null : _mapper.Map<RestoranMenuKategoriDto>(entity);
    }

    public override async Task<RestoranMenuKategoriDto> AddAsync(RestoranMenuKategoriDto request)
    {
        Validate(request.RestoranId, request.Ad!);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId);

        var restoranExists = await _restoranRepository.AnyAsync(x => x.Id == request.RestoranId);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 400);
        }

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _kategoriRepository.AnyAsync(x => x.RestoranId == request.RestoranId && x.Ad.ToUpper() == normalizedAd && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni adla aktif kategori zaten var.", 400);
        }

        request.Ad = request.Ad.Trim();
        return await base.AddAsync(request);
    }

    public override async Task<RestoranMenuKategoriDto> UpdateAsync(RestoranMenuKategoriDto request)
    {
        if (!request.Id.HasValue)
        {
            throw new BaseException("Menu kategorisi id zorunludur.", 400);
        }

        Validate(request.RestoranId, request.Ad!);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId);

        var entity = await _kategoriRepository.GetByIdAsync(request.Id.Value)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 404);
        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _kategoriRepository.AnyAsync(x => x.Id != request.Id.Value && x.RestoranId == request.RestoranId && x.Ad.ToUpper() == normalizedAd && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni adla aktif kategori zaten var.", 400);
        }

        request.Ad = request.Ad.Trim();
        return await base.UpdateAsync(request);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _kategoriRepository.GetByIdAsync(id)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 404);

        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        await base.DeleteAsync(id);
    }

    public async Task<RestoranMenuDto> GetMenuByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);

        var kategoriler = await _kategoriRepository.GetByRestoranIdWithUrunlerAsync(restoranId, cancellationToken);

        return new RestoranMenuDto
        {
            RestoranId = restoranId,
            Kategoriler = kategoriler
                .Where(x => x.AktifMi)
                .Select(x => new RestoranMenuKategoriDetayDto
                {
                    Id = x.Id,
                    Ad = x.Ad,
                    SiraNo = x.SiraNo,
                    Urunler = x.Urunler
                        .Where(u => u.AktifMi)
                        .OrderBy(u => u.Ad)
                        .ThenBy(u => u.Id)
                        .Select(u => new RestoranMenuUrunDetayDto
                        {
                            Id = u.Id,
                            Ad = u.Ad,
                            Aciklama = u.Aciklama,
                            Fiyat = u.Fiyat,
                            ParaBirimi = u.ParaBirimi,
                            HazirlamaSuresiDakika = u.HazirlamaSuresiDakika
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public async Task<List<RestoranGlobalMenuKategoriDto>> GetGlobalListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureGlobalTableBackfillAsync(cancellationToken);
        var categories = await LoadGlobalCategoriesAsync(cancellationToken);
        return categories.Select(ToGlobalDto).ToList();
    }

    public async Task<RestoranGlobalMenuKategoriDto> CreateGlobalAsync(CreateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default)
    {
        var ad = NormalizeAd(request.Ad);
        ValidateGlobal(ad);

        await EnsureGlobalTableBackfillAsync(cancellationToken);

        var existingByName = await LoadGlobalCategoriesAsync(cancellationToken);
        if (existingByName.Any(x => x.Ad.Equals(ad, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BaseException("Ayni adla global kategori zaten var.", 400);
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [restoran].[MenuKategoriTanimlari]
            ([Ad], [SiraNo], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            VALUES ({ad}, {request.SiraNo}, {request.AktifMi}, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), N'system', N'system');
            """,
            cancellationToken);

        await SyncGlobalCategoryToRestaurantsAsync(ad, request.SiraNo, request.AktifMi, cancellationToken);

        var created = (await LoadGlobalCategoriesAsync(cancellationToken))
            .FirstOrDefault(x => x.Ad.Equals(ad, StringComparison.OrdinalIgnoreCase))
            ?? throw new BaseException("Global kategori olusturulamadi.", 500);

        return ToGlobalDto(created);
    }

    public async Task<RestoranGlobalMenuKategoriDto> UpdateGlobalAsync(int id, UpdateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken = default)
    {
        var ad = NormalizeAd(request.Ad);
        ValidateGlobal(ad);

        await EnsureGlobalTableBackfillAsync(cancellationToken);
        var categories = await LoadGlobalCategoriesAsync(cancellationToken);
        var current = categories.FirstOrDefault(x => x.Id == id) ?? throw new BaseException("Global kategori bulunamadi.", 404);

        if (categories.Any(x => x.Id != id && x.Ad.Equals(ad, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BaseException("Ayni adla global kategori zaten var.", 400);
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [restoran].[MenuKategoriTanimlari]
            SET [Ad] = {ad},
                [SiraNo] = {request.SiraNo},
                [AktifMi] = {request.AktifMi},
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'system'
            WHERE [Id] = {id}
              AND [IsDeleted] = 0;
            """,
            cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [restoran].[RestoranMenuKategorileri]
            SET [Ad] = {ad},
                [SiraNo] = {request.SiraNo},
                [AktifMi] = {request.AktifMi},
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'system'
            WHERE [Ad] = {current.Ad}
              AND [IsDeleted] = 0;
            """,
            cancellationToken);

        var updated = (await LoadGlobalCategoriesAsync(cancellationToken))
            .FirstOrDefault(x => x.Id == id)
            ?? throw new BaseException("Global kategori guncellenemedi.", 500);

        return ToGlobalDto(updated);
    }

    public async Task DeleteGlobalAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureGlobalTableBackfillAsync(cancellationToken);
        var categories = await LoadGlobalCategoriesAsync(cancellationToken);
        var current = categories.FirstOrDefault(x => x.Id == id) ?? throw new BaseException("Global kategori bulunamadi.", 404);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [restoran].[MenuKategoriTanimlari]
            SET [AktifMi] = 0,
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'system'
            WHERE [Id] = {id}
              AND [IsDeleted] = 0;
            """,
            cancellationToken);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE [restoran].[RestoranMenuKategorileri]
            SET [AktifMi] = 0,
                [UpdatedAt] = SYSUTCDATETIME(),
                [UpdatedBy] = N'system'
            WHERE [Ad] = {current.Ad}
              AND [IsDeleted] = 0;
            """,
            cancellationToken);
    }

    public async Task<RestoranKategoriAtamaBaglamDto> GetAtamaBaglamAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);

        var restoranExists = await _restoranRepository.AnyAsync(x => x.Id == restoranId && x.AktifMi);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 404);
        }

        await EnsureGlobalTableBackfillAsync(cancellationToken);
        var globalCategories = await LoadGlobalCategoriesAsync(cancellationToken);

        var selectedNames = await _kategoriRepository.Where(x => x.RestoranId == restoranId && x.AktifMi)
            .Select(x => x.Ad)
            .Distinct()
            .ToListAsync(cancellationToken);

        var selectedIds = globalCategories
            .Where(x => selectedNames.Contains(x.Ad))
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return new RestoranKategoriAtamaBaglamDto
        {
            RestoranId = restoranId,
            GlobalKategoriler = globalCategories.Select(ToGlobalDto).ToList(),
            SeciliGlobalKategoriIdleri = selectedIds
        };
    }

    public async Task<RestoranKategoriAtamaBaglamDto> SaveAtamalarAsync(SaveRestoranKategoriAtamaRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RestoranId <= 0)
        {
            throw new BaseException("Restoran secimi zorunludur.", 400);
        }

        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId, cancellationToken);

        var restoranExists = await _restoranRepository.AnyAsync(x => x.Id == request.RestoranId && x.AktifMi);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 404);
        }

        await EnsureGlobalTableBackfillAsync(cancellationToken);
        var globalCategories = await LoadGlobalCategoriesAsync(cancellationToken);
        var selectedGlobals = globalCategories.Where(x => request.SeciliGlobalKategoriIdleri.Contains(x.Id)).ToList();

        var restaurantCategories = await _kategoriRepository.Where(x => x.RestoranId == request.RestoranId)
            .ToListAsync(cancellationToken);

        foreach (var global in selectedGlobals)
        {
            var existing = restaurantCategories.FirstOrDefault(x => x.Ad.Equals(global.Ad, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new RestoranMenuKategori
                {
                    RestoranId = request.RestoranId,
                    Ad = global.Ad,
                    SiraNo = global.SiraNo,
                    AktifMi = global.AktifMi
                };
                await _kategoriRepository.AddAsync(existing);
                restaurantCategories.Add(existing);
            }
            else
            {
                existing.SiraNo = global.SiraNo;
                existing.AktifMi = global.AktifMi;
            }
        }

        var selectedNames = selectedGlobals.Select(x => x.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var globalNames = globalCategories.Select(x => x.Ad).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var category in restaurantCategories.Where(x => globalNames.Contains(x.Ad)))
        {
            if (!selectedNames.Contains(category.Ad))
            {
                category.AktifMi = false;
            }
        }

        await _kategoriRepository.SaveChangesAsync();
        return await GetAtamaBaglamAsync(request.RestoranId, cancellationToken);
    }

    private async Task SyncGlobalCategoryToRestaurantsAsync(string ad, int siraNo, bool aktifMi, CancellationToken cancellationToken)
    {
        var activeRestoranlar = await _restoranRepository.Where(x => x.AktifMi).ToListAsync(cancellationToken);
        foreach (var restoran in activeRestoranlar)
        {
            var existing = await _kategoriRepository.Where(x => x.RestoranId == restoran.Id && x.Ad.ToUpper() == ad.ToUpper())
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                await _kategoriRepository.AddAsync(new RestoranMenuKategori
                {
                    RestoranId = restoran.Id,
                    Ad = ad,
                    SiraNo = siraNo,
                    AktifMi = aktifMi
                });
            }
            else
            {
                existing.SiraNo = siraNo;
                existing.AktifMi = aktifMi;
            }
        }

        await _kategoriRepository.SaveChangesAsync();
    }

    private async Task EnsureGlobalTableBackfillAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [restoran].[MenuKategoriTanimlari]
            ([Ad], [SiraNo], [AktifMi], [IsDeleted], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy])
            SELECT
                k.[Ad],
                MIN(k.[SiraNo]) AS [SiraNo],
                CAST(MAX(CASE WHEN k.[AktifMi] = 1 THEN 1 ELSE 0 END) AS bit) AS [AktifMi],
                0,
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                N'system',
                N'system'
            FROM [restoran].[RestoranMenuKategorileri] k
            WHERE k.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [restoran].[MenuKategoriTanimlari] g
                  WHERE g.[Ad] = k.[Ad]
                    AND g.[IsDeleted] = 0
              )
            GROUP BY k.[Ad];
            """,
            cancellationToken);
    }

    private async Task<List<GlobalCategoryRow>> LoadGlobalCategoriesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Database.SqlQueryRaw<GlobalCategoryRow>(
            """
            SELECT
                g.[Id],
                g.[Ad],
                g.[SiraNo],
                g.[AktifMi],
                (
                    SELECT COUNT(DISTINCT k.[RestoranId])
                    FROM [restoran].[RestoranMenuKategorileri] k
                    WHERE k.[Ad] = g.[Ad]
                      AND k.[IsDeleted] = 0
                      AND k.[AktifMi] = 1
                ) AS [RestoranSayisi]
            FROM [restoran].[MenuKategoriTanimlari] g
            WHERE g.[IsDeleted] = 0
            ORDER BY g.[SiraNo], g.[Ad], g.[Id];
            """)
            .ToListAsync(cancellationToken);
    }

    private static RestoranGlobalMenuKategoriDto ToGlobalDto(GlobalCategoryRow category)
    {
        return new RestoranGlobalMenuKategoriDto
        {
            Id = category.Id,
            Ad = category.Ad,
            SiraNo = category.SiraNo,
            AktifMi = category.AktifMi,
            RestoranSayisi = category.RestoranSayisi
        };
    }

    private static void Validate(int restoranId, string ad)
    {
        if (restoranId <= 0)
        {
            throw new BaseException("Restoran secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(ad))
        {
            throw new BaseException("Kategori adi zorunludur.", 400);
        }
    }

    private static void ValidateGlobal(string ad)
    {
        if (string.IsNullOrWhiteSpace(ad))
        {
            throw new BaseException("Kategori adi zorunludur.", 400);
        }
    }

    private static string NormalizeAd(string ad)
        => ad.Trim();

    private sealed class GlobalCategoryRow
    {
        public int Id { get; init; }
        public string Ad { get; init; } = string.Empty;
        public int SiraNo { get; init; }
        public bool AktifMi { get; init; }
        public int RestoranSayisi { get; init; }
    }
}

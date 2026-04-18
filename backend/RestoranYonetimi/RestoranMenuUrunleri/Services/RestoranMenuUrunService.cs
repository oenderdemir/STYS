using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.RestoranMenuKategorileri.Repositories;
using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Entities;
using STYS.RestoranMenuUrunleri.Repositories;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranMenuUrunleri.Services;

public class RestoranMenuUrunService : BaseRdbmsService<RestoranMenuUrunDto, RestoranMenuUrun, int>, IRestoranMenuUrunService
{
    private readonly IRestoranMenuUrunRepository _urunRepository;
    private readonly IRestoranMenuKategoriRepository _kategoriRepository;
    private readonly IMapper _mapper;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranMenuUrunService(
        IRestoranMenuUrunRepository urunRepository,
        IRestoranMenuKategoriRepository kategoriRepository,
        IMapper mapper,
        IRestoranErisimService restoranErisimService)
        : base(urunRepository, mapper)
    {
        _urunRepository = urunRepository;
        _kategoriRepository = kategoriRepository;
        _mapper = mapper;
        _restoranErisimService = restoranErisimService;
    }

    public override async Task<IEnumerable<RestoranMenuUrunDto>> GetAllAsync(Func<IQueryable<RestoranMenuUrun>, IQueryable<RestoranMenuUrun>>? include = null)
    {
        var query = _urunRepository.Where(x => true);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => x.RestoranMenuKategori != null && yetkiliRestoranlar.Contains(x.RestoranMenuKategori.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.Ad).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMenuUrunDto>>(items);
    }

    public override async Task<IEnumerable<RestoranMenuUrunDto>> WhereAsync(
        Expression<Func<RestoranMenuUrun, bool>> predicate,
        Func<IQueryable<RestoranMenuUrun>, IQueryable<RestoranMenuUrun>>? include = null)
    {
        var query = _urunRepository.Where(predicate);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => x.RestoranMenuKategori != null && yetkiliRestoranlar.Contains(x.RestoranMenuKategori.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.Ad).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMenuUrunDto>>(items);
    }

    public override async Task<RestoranMenuUrunDto?> GetByIdAsync(int id, Func<IQueryable<RestoranMenuUrun>, IQueryable<RestoranMenuUrun>>? include = null)
    {
        var includeQuery = include is null
            ? (Func<IQueryable<RestoranMenuUrun>, IQueryable<RestoranMenuUrun>>)(query => query.Include(x => x.RestoranMenuKategori))
            : query => include(query).Include(x => x.RestoranMenuKategori);

        var entity = await _urunRepository.GetByIdAsync(id, includeQuery);
        if (entity?.RestoranMenuKategori is not null)
        {
            await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranMenuKategori.RestoranId);
        }

        return entity is null ? null : _mapper.Map<RestoranMenuUrunDto>(entity);
    }

    public override async Task<RestoranMenuUrunDto> AddAsync(RestoranMenuUrunDto request)
    {
        Validate(request.RestoranMenuKategoriId, request.Ad!, request.Fiyat, request.ParaBirimi!);

        var kategori = await _kategoriRepository.GetByIdAsync(request.RestoranMenuKategoriId)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 400);
        await _restoranErisimService.EnsureRestoranErisimiAsync(kategori.RestoranId);

        request.Ad = request.Ad.Trim();
        request.Aciklama = NormalizeOptional(request.Aciklama, 512);
        request.ParaBirimi = request.ParaBirimi.Trim().ToUpperInvariant();
        return await base.AddAsync(request);
    }

    public override async Task<RestoranMenuUrunDto> UpdateAsync(RestoranMenuUrunDto request)
    {
        if (!request.Id.HasValue)
        {
            throw new BaseException("Menu urunu id zorunludur.", 400);
        }

        Validate(request.RestoranMenuKategoriId, request.Ad!, request.Fiyat, request.ParaBirimi!);

        var entity = await _urunRepository.GetByIdAsync(request.Id.Value)
            ?? throw new BaseException("Menu urunu bulunamadi.", 404);

        var currentKategori = await _kategoriRepository.GetByIdAsync(entity.RestoranMenuKategoriId)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 400);
        await _restoranErisimService.EnsureRestoranErisimiAsync(currentKategori.RestoranId);

        var targetKategori = await _kategoriRepository.GetByIdAsync(request.RestoranMenuKategoriId)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 400);
        await _restoranErisimService.EnsureRestoranErisimiAsync(targetKategori.RestoranId);

        request.Ad = request.Ad.Trim();
        request.Aciklama = NormalizeOptional(request.Aciklama, 512);
        request.ParaBirimi = request.ParaBirimi.Trim().ToUpperInvariant();
        return await base.UpdateAsync(request);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _urunRepository.GetByIdAsync(id)
            ?? throw new BaseException("Menu urunu bulunamadi.", 404);

        var kategori = await _kategoriRepository.GetByIdAsync(entity.RestoranMenuKategoriId)
            ?? throw new BaseException("Menu kategorisi bulunamadi.", 400);
        await _restoranErisimService.EnsureRestoranErisimiAsync(kategori.RestoranId);

        await base.DeleteAsync(id);
    }

    private static void Validate(int kategoriId, string ad, decimal fiyat, string paraBirimi)
    {
        if (kategoriId <= 0)
        {
            throw new BaseException("Kategori secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(ad))
        {
            throw new BaseException("Urun adi zorunludur.", 400);
        }

        if (fiyat < 0)
        {
            throw new BaseException("Urun fiyati negatif olamaz.", 400);
        }

        if (string.IsNullOrWhiteSpace(paraBirimi) || paraBirimi.Trim().Length != 3)
        {
            throw new BaseException("Para birimi 3 haneli olmalidir.", 400);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}

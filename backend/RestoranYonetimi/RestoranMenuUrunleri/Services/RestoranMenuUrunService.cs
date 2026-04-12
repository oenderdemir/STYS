using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranMenuUrunleri.Services;

public class RestoranMenuUrunService : IRestoranMenuUrunService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;

    public RestoranMenuUrunService(StysAppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<List<RestoranMenuUrunDto>> GetListAsync(int? kategoriId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RestoranMenuUrunleri.AsQueryable();
        if (kategoriId.HasValue && kategoriId.Value > 0)
        {
            query = query.Where(x => x.RestoranMenuKategoriId == kategoriId.Value);
        }

        var items = await query.OrderBy(x => x.Ad).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return _mapper.Map<List<RestoranMenuUrunDto>>(items);
    }

    public async Task<RestoranMenuUrunDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RestoranMenuUrunleri.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<RestoranMenuUrunDto>(entity);
    }

    public async Task<RestoranMenuUrunDto> CreateAsync(CreateRestoranMenuUrunRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.RestoranMenuKategoriId, request.Ad, request.Fiyat, request.ParaBirimi);

        var kategoriExists = await _dbContext.RestoranMenuKategorileri.AnyAsync(x => x.Id == request.RestoranMenuKategoriId, cancellationToken);
        if (!kategoriExists)
        {
            throw new BaseException("Menu kategorisi bulunamadi.", 400);
        }

        var entity = new RestoranMenuUrun
        {
            RestoranMenuKategoriId = request.RestoranMenuKategoriId,
            Ad = request.Ad.Trim(),
            Aciklama = NormalizeOptional(request.Aciklama, 512),
            Fiyat = request.Fiyat,
            ParaBirimi = request.ParaBirimi.Trim().ToUpperInvariant(),
            HazirlamaSuresiDakika = request.HazirlamaSuresiDakika,
            AktifMi = request.AktifMi
        };

        _dbContext.RestoranMenuUrunleri.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<RestoranMenuUrunDto>(entity);
    }

    public async Task<RestoranMenuUrunDto> UpdateAsync(int id, UpdateRestoranMenuUrunRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.RestoranMenuKategoriId, request.Ad, request.Fiyat, request.ParaBirimi);

        var entity = await _dbContext.RestoranMenuUrunleri.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Menu urunu bulunamadi.", 404);

        entity.RestoranMenuKategoriId = request.RestoranMenuKategoriId;
        entity.Ad = request.Ad.Trim();
        entity.Aciklama = NormalizeOptional(request.Aciklama, 512);
        entity.Fiyat = request.Fiyat;
        entity.ParaBirimi = request.ParaBirimi.Trim().ToUpperInvariant();
        entity.HazirlamaSuresiDakika = request.HazirlamaSuresiDakika;
        entity.AktifMi = request.AktifMi;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<RestoranMenuUrunDto>(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RestoranMenuUrunleri.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Menu urunu bulunamadi.", 404);

        _dbContext.RestoranMenuUrunleri.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
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

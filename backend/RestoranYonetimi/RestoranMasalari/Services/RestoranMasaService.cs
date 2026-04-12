using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMasalari.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranMasalari.Services;

public class RestoranMasaService : IRestoranMasaService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranMasaRepository _masaRepository;
    private readonly IMapper _mapper;

    public RestoranMasaService(StysAppDbContext dbContext, IRestoranMasaRepository masaRepository, IMapper mapper)
    {
        _dbContext = dbContext;
        _masaRepository = masaRepository;
        _mapper = mapper;
    }

    public async Task<List<RestoranMasaDto>> GetListAsync(int? restoranId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RestoranMasalari.AsQueryable();
        if (restoranId.HasValue && restoranId.Value > 0)
        {
            query = query.Where(x => x.RestoranId == restoranId.Value);
        }

        var items = await query.OrderBy(x => x.MasaNo).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        return _mapper.Map<List<RestoranMasaDto>>(items);
    }

    public async Task<List<RestoranMasaDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        if (restoranId <= 0)
        {
            return [];
        }

        var items = await _masaRepository.GetByRestoranIdAsync(restoranId, cancellationToken);
        return _mapper.Map<List<RestoranMasaDto>>(items);
    }

    public async Task<RestoranMasaDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RestoranMasalari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : _mapper.Map<RestoranMasaDto>(entity);
    }

    public async Task<RestoranMasaDto> CreateAsync(CreateRestoranMasaRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.RestoranId, request.MasaNo, request.Kapasite, request.Durum);

        var restoranExists = await _dbContext.Restoranlar.AnyAsync(x => x.Id == request.RestoranId, cancellationToken);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 400);
        }

        var normalizedMasaNo = request.MasaNo.Trim().ToUpperInvariant();
        var exists = await _dbContext.RestoranMasalari.AnyAsync(x => x.RestoranId == request.RestoranId && x.MasaNo.ToUpper() == normalizedMasaNo && x.AktifMi, cancellationToken);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni masa no ile aktif masa zaten var.", 400);
        }

        var entity = new RestoranMasa
        {
            RestoranId = request.RestoranId,
            MasaNo = request.MasaNo.Trim(),
            Kapasite = request.Kapasite,
            Durum = request.Durum.Trim(),
            AktifMi = request.AktifMi
        };

        _dbContext.RestoranMasalari.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<RestoranMasaDto>(entity);
    }

    public async Task<RestoranMasaDto> UpdateAsync(int id, UpdateRestoranMasaRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.RestoranId, request.MasaNo, request.Kapasite, request.Durum);

        var entity = await _dbContext.RestoranMasalari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Masa bulunamadi.", 404);

        var restoranExists = await _dbContext.Restoranlar.AnyAsync(x => x.Id == request.RestoranId, cancellationToken);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 400);
        }

        var normalizedMasaNo = request.MasaNo.Trim().ToUpperInvariant();
        var exists = await _dbContext.RestoranMasalari.AnyAsync(x => x.Id != id && x.RestoranId == request.RestoranId && x.MasaNo.ToUpper() == normalizedMasaNo && x.AktifMi, cancellationToken);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni masa no ile aktif masa zaten var.", 400);
        }

        entity.RestoranId = request.RestoranId;
        entity.MasaNo = request.MasaNo.Trim();
        entity.Kapasite = request.Kapasite;
        entity.Durum = request.Durum.Trim();
        entity.AktifMi = request.AktifMi;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<RestoranMasaDto>(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.RestoranMasalari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Masa bulunamadi.", 404);

        _dbContext.RestoranMasalari.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(int restoranId, string masaNo, int kapasite, string durum)
    {
        if (restoranId <= 0)
        {
            throw new BaseException("Restoran secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(masaNo))
        {
            throw new BaseException("Masa no zorunludur.", 400);
        }

        if (kapasite <= 0)
        {
            throw new BaseException("Kapasite sifirdan buyuk olmalidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(durum))
        {
            throw new BaseException("Masa durumu zorunludur.", 400);
        }
    }
}

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranMasalari.Repositories;
using STYS.Restoranlar.Repositories;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranMasalari.Services;

public class RestoranMasaService : BaseRdbmsService<RestoranMasaDto, RestoranMasa, int>, IRestoranMasaService
{
    private readonly IRestoranMasaRepository _masaRepository;
    private readonly IRestoranRepository _restoranRepository;
    private readonly IMapper _mapper;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranMasaService(
        IRestoranMasaRepository masaRepository,
        IRestoranRepository restoranRepository,
        IMapper mapper,
        IRestoranErisimService restoranErisimService)
        : base(masaRepository, mapper)
    {
        _masaRepository = masaRepository;
        _restoranRepository = restoranRepository;
        _mapper = mapper;
        _restoranErisimService = restoranErisimService;
    }

    public override async Task<IEnumerable<RestoranMasaDto>> GetAllAsync(Func<IQueryable<RestoranMasa>, IQueryable<RestoranMasa>>? include = null)
    {
        var query = _masaRepository.Where(x => true);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.MasaNo).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMasaDto>>(items);
    }

    public override async Task<IEnumerable<RestoranMasaDto>> WhereAsync(
        Expression<Func<RestoranMasa, bool>> predicate,
        Func<IQueryable<RestoranMasa>, IQueryable<RestoranMasa>>? include = null)
    {
        var query = _masaRepository.Where(predicate);
        var yetkiliRestoranlar = await _restoranErisimService.GetYetkiliRestoranIdleriAsync();
        if (yetkiliRestoranlar is not null)
        {
            query = query.Where(x => yetkiliRestoranlar.Contains(x.RestoranId));
        }

        if (include is not null)
        {
            query = include(query);
        }

        var items = await query.OrderBy(x => x.MasaNo).ThenBy(x => x.Id).ToListAsync();
        return _mapper.Map<List<RestoranMasaDto>>(items);
    }

    public override async Task<RestoranMasaDto?> GetByIdAsync(int id, Func<IQueryable<RestoranMasa>, IQueryable<RestoranMasa>>? include = null)
    {
        var entity = await _masaRepository.GetByIdAsync(id, include);
        if (entity is not null)
        {
            await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        }

        return entity is null ? null : _mapper.Map<RestoranMasaDto>(entity);
    }

    public async Task<List<RestoranMasaDto>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        if (restoranId <= 0)
        {
            return [];
        }

        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);
        var items = await _masaRepository.GetByRestoranIdAsync(restoranId, cancellationToken);
        return _mapper.Map<List<RestoranMasaDto>>(items);
    }

    public override async Task<RestoranMasaDto> AddAsync(RestoranMasaDto request)
    {
        Validate(request.RestoranId, request.MasaNo, request.Kapasite, request.Durum!);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId);

        var restoranExists = await _restoranRepository.AnyAsync(x => x.Id == request.RestoranId);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 400);
        }

        var normalizedMasaNo = request.MasaNo.Trim().ToUpperInvariant();
        var exists = await _masaRepository.AnyAsync(x => x.RestoranId == request.RestoranId && x.MasaNo.ToUpper() == normalizedMasaNo && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni masa no ile aktif masa zaten var.", 400);
        }

        request.MasaNo = request.MasaNo.Trim();
        request.Durum = request.Durum.Trim();
        return await base.AddAsync(request);
    }

    public override async Task<RestoranMasaDto> UpdateAsync(RestoranMasaDto request)
    {
        if (!request.Id.HasValue)
        {
            throw new BaseException("Masa id zorunludur.", 400);
        }

        Validate(request.RestoranId, request.MasaNo, request.Kapasite, request.Durum!);

        var entity = await _masaRepository.GetByIdAsync(request.Id.Value)
            ?? throw new BaseException("Masa bulunamadi.", 404);

        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.RestoranId);

        var restoranExists = await _restoranRepository.AnyAsync(x => x.Id == request.RestoranId);
        if (!restoranExists)
        {
            throw new BaseException("Restoran bulunamadi.", 400);
        }

        var normalizedMasaNo = request.MasaNo.Trim().ToUpperInvariant();
        var exists = await _masaRepository.AnyAsync(x => x.Id != request.Id.Value && x.RestoranId == request.RestoranId && x.MasaNo.ToUpper() == normalizedMasaNo && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni restoran altinda ayni masa no ile aktif masa zaten var.", 400);
        }

        request.MasaNo = request.MasaNo.Trim();
        request.Durum = request.Durum.Trim();
        return await base.UpdateAsync(request);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _masaRepository.GetByIdAsync(id)
            ?? throw new BaseException("Masa bulunamadi.", 404);

        await _restoranErisimService.EnsureRestoranErisimiAsync(entity.RestoranId);
        await base.DeleteAsync(id);
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

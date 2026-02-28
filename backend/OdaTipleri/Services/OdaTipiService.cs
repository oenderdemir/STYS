using AutoMapper;
using STYS.OdaSiniflari.Repositories;
using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaTipleri.Services;

public class OdaTipiService : BaseRdbmsService<OdaTipiDto, OdaTipi, int>, IOdaTipiService
{
    private readonly IOdaTipiRepository _odaTipiRepository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IOdaSinifiRepository _odaSinifiRepository;

    public OdaTipiService(
        IOdaTipiRepository odaTipiRepository,
        ITesisRepository tesisRepository,
        IOdaSinifiRepository odaSinifiRepository,
        IMapper mapper)
        : base(odaTipiRepository, mapper)
    {
        _odaTipiRepository = odaTipiRepository;
        _tesisRepository = tesisRepository;
        _odaSinifiRepository = odaSinifiRepository;
    }

    public override async Task<OdaTipiDto> AddAsync(OdaTipiDto dto)
    {
        Normalize(dto);
        await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveNameAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<OdaTipiDto> UpdateAsync(OdaTipiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda tipi id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureDependenciesAsync(dto);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureDependenciesAsync(OdaTipiDto dto)
    {
        var tesis = await _tesisRepository.GetByIdAsync(dto.TesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 400);
        }

        if (!tesis.AktifMi)
        {
            throw new BaseException("Pasif tesis altinda oda tipi olusturulamaz veya guncellenemez.", 400);
        }

        var odaSinifi = await _odaSinifiRepository.GetByIdAsync(dto.OdaSinifiId);
        if (odaSinifi is null)
        {
            throw new BaseException("Secilen oda sinifi bulunamadi.", 400);
        }

        if (!odaSinifi.AktifMi)
        {
            throw new BaseException("Pasif oda sinifi secilemez.", 400);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(OdaTipiDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _odaTipiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.TesisId == dto.TesisId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni isimde aktif oda tipi zaten mevcut.", 400);
        }
    }

    private static void Normalize(OdaTipiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Oda tipi adi zorunludur.", 400);
        }

        if (dto.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (dto.OdaSinifiId <= 0)
        {
            throw new BaseException("Oda sinifi secimi zorunludur.", 400);
        }

        if (dto.Kapasite <= 0)
        {
            throw new BaseException("Kapasite sifirdan buyuk olmalidir.", 400);
        }

        if (dto.Metrekare.HasValue && dto.Metrekare.Value <= 0)
        {
            throw new BaseException("Metrekare sifirdan buyuk olmalidir.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }
}

using AutoMapper;
using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaTipleri.Services;

public class OdaTipiService : BaseRdbmsService<OdaTipiDto, OdaTipi, int>, IOdaTipiService
{
    private readonly IOdaTipiRepository _odaTipiRepository;

    public OdaTipiService(IOdaTipiRepository odaTipiRepository, IMapper mapper)
        : base(odaTipiRepository, mapper)
    {
        _odaTipiRepository = odaTipiRepository;
    }

    public override async Task<OdaTipiDto> AddAsync(OdaTipiDto dto)
    {
        Normalize(dto);
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
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
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
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni isimde aktif oda tipi zaten mevcut.", 400);
        }
    }

    private static void Normalize(OdaTipiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Oda tipi adi zorunludur.", 400);
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
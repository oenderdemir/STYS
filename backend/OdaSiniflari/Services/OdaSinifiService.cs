using AutoMapper;
using STYS.OdaSiniflari.Dto;
using STYS.OdaSiniflari.Entities;
using STYS.OdaSiniflari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaSiniflari.Services;

public class OdaSinifiService : BaseRdbmsService<OdaSinifiDto, OdaSinifi, int>, IOdaSinifiService
{
    private readonly IOdaSinifiRepository _odaSinifiRepository;

    public OdaSinifiService(IOdaSinifiRepository odaSinifiRepository, IMapper mapper)
        : base(odaSinifiRepository, mapper)
    {
        _odaSinifiRepository = odaSinifiRepository;
    }

    public override async Task<OdaSinifiDto> AddAsync(OdaSinifiDto dto)
    {
        Normalize(dto);
        await EnsureUniqueActiveAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<OdaSinifiDto> UpdateAsync(OdaSinifiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda sinifi id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureUniqueActiveAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureUniqueActiveAsync(OdaSinifiDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedCode = dto.Kod.Trim().ToUpperInvariant();
        var normalizedName = dto.Ad.Trim().ToUpperInvariant();

        var codeExists = await _odaSinifiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Kod.ToUpper() == normalizedCode &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (codeExists)
        {
            throw new BaseException("Ayni kodda aktif oda sinifi zaten mevcut.", 400);
        }

        var nameExists = await _odaSinifiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (nameExists)
        {
            throw new BaseException("Ayni isimde aktif oda sinifi zaten mevcut.", 400);
        }
    }

    private static void Normalize(OdaSinifiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Oda sinifi kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Oda sinifi adi zorunludur.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
    }
}

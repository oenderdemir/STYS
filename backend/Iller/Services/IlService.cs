using AutoMapper;
using STYS.Iller.Dto;
using STYS.Iller.Entities;
using STYS.Iller.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Iller.Services;

public class IlService : BaseRdbmsService<IlDto, Il, int>, IIlService
{
    private readonly IIlRepository _ilRepository;

    public IlService(IIlRepository ilRepository, IMapper mapper)
        : base(ilRepository, mapper)
    {
        _ilRepository = ilRepository;
    }

    public override async Task<IlDto> AddAsync(IlDto dto)
    {
        Normalize(dto);
        await EnsureUniqueActiveNameAsync(dto.Ad, dto.AktifMi);
        return await base.AddAsync(dto);
    }

    public override async Task<IlDto> UpdateAsync(IlDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Il id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureUniqueActiveNameAsync(dto.Ad, dto.AktifMi, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureUniqueActiveNameAsync(string name, bool isActive, int? excludedId = null)
    {
        if (!isActive)
        {
            return;
        }

        var normalizedName = name.Trim().ToUpperInvariant();
        var exists = await _ilRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni isimde aktif il zaten mevcut.", 400);
        }
    }

    private static void Normalize(IlDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Il adi zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }
}
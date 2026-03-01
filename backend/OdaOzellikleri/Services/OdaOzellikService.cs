using AutoMapper;
using STYS.OdaOzellikleri.Dto;
using STYS.OdaOzellikleri.Entities;
using STYS.OdaOzellikleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.OdaOzellikleri.Services;

public class OdaOzellikService : BaseRdbmsService<OdaOzellikDto, OdaOzellik, int>, IOdaOzellikService
{
    private readonly IOdaOzellikRepository _odaOzellikRepository;

    public OdaOzellikService(IOdaOzellikRepository odaOzellikRepository, IMapper mapper)
        : base(odaOzellikRepository, mapper)
    {
        _odaOzellikRepository = odaOzellikRepository;
    }

    public override async Task<OdaOzellikDto> AddAsync(OdaOzellikDto dto)
    {
        Normalize(dto);
        await EnsureUniqueActiveAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<OdaOzellikDto> UpdateAsync(OdaOzellikDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda ozellik id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureUniqueActiveAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureUniqueActiveAsync(OdaOzellikDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedCode = dto.Kod.Trim().ToUpperInvariant();
        var normalizedName = dto.Ad.Trim().ToUpperInvariant();

        var codeExists = await _odaOzellikRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Kod.ToUpper() == normalizedCode &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (codeExists)
        {
            throw new BaseException("Ayni kodda aktif oda ozelligi zaten mevcut.", 400);
        }

        var nameExists = await _odaOzellikRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (nameExists)
        {
            throw new BaseException("Ayni isimde aktif oda ozelligi zaten mevcut.", 400);
        }
    }

    private static void Normalize(OdaOzellikDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Oda ozellik kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Oda ozellik adi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.VeriTipi))
        {
            throw new BaseException("Veri tipi zorunludur.", 400);
        }

        var normalizedVeriTipi = dto.VeriTipi.Trim().ToLowerInvariant();
        if (!OdaOzellikVeriTipleri.All.Contains(normalizedVeriTipi))
        {
            throw new BaseException("Gecersiz veri tipi secildi.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.VeriTipi = normalizedVeriTipi;
    }
}

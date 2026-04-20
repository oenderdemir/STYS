using AutoMapper;
using STYS.Muhasebe.PaketTurleri.Dtos;
using STYS.Muhasebe.PaketTurleri.Entities;
using STYS.Muhasebe.PaketTurleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.PaketTurleri.Services;

public class PaketTuruService : BaseRdbmsService<PaketTuruDto, PaketTuru, int>, IPaketTuruService
{
    private readonly IPaketTuruRepository _repository;

    public PaketTuruService(IPaketTuruRepository repository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
    }

    public override async Task<PaketTuruDto> AddAsync(PaketTuruDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<PaketTuruDto> UpdateAsync(PaketTuruDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Paket turu id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task NormalizeAndValidateAsync(PaketTuruDto dto, int? currentId)
    {
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.KisaAd = dto.KisaAd?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Paket turu adi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.KisaAd))
        {
            throw new BaseException("Kisa ad zorunludur.", 400);
        }

        var hasNameDuplicate = await _repository.AnyAsync(x =>
            x.Ad == dto.Ad &&
            (!currentId.HasValue || x.Id != currentId.Value));

        if (hasNameDuplicate)
        {
            throw new BaseException("Ayni isimde paket turu zaten var.", 400);
        }

        var hasShortNameDuplicate = await _repository.AnyAsync(x =>
            x.KisaAd == dto.KisaAd &&
            (!currentId.HasValue || x.Id != currentId.Value));

        if (hasShortNameDuplicate)
        {
            throw new BaseException("Ayni kisa adda paket turu zaten var.", 400);
        }
    }
}

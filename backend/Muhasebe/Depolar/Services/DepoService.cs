using AutoMapper;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Depolar.Services;

public class DepoService : BaseRdbmsService<DepoDto, Depo, int>, IDepoService
{
    private readonly IDepoRepository _repository;
    private readonly ITesisRepository _tesisRepository;

    public DepoService(IDepoRepository repository, ITesisRepository tesisRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _tesisRepository = tesisRepository;
    }

    public override async Task<DepoDto> AddAsync(DepoDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<DepoDto> UpdateAsync(DepoDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Depo id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id);
        return await base.UpdateAsync(dto);
    }

    private async Task NormalizeAndValidateAsync(DepoDto dto, int? currentId)
    {
        dto.Kod = dto.Kod?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Depo kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Depo adi zorunludur.", 400);
        }

        if (dto.TesisId.HasValue && dto.TesisId.Value > 0)
        {
            var tesisExists = await _tesisRepository.AnyAsync(x => x.Id == dto.TesisId.Value);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        var duplicate = await _repository.AnyAsync(x => x.Kod == dto.Kod && (!currentId.HasValue || x.Id != currentId.Value));
        if (duplicate)
        {
            throw new BaseException("Depo kodu benzersiz olmalidir.", 400);
        }
    }
}

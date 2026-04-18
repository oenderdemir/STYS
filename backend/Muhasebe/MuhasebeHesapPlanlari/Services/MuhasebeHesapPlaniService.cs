using AutoMapper;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Services;

public class MuhasebeHesapPlaniService : BaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>, IMuhasebeHesapPlaniService
{
    private readonly IMuhasebeHesapPlaniRepository _repository;

    public MuhasebeHesapPlaniService(IMuhasebeHesapPlaniRepository repository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
    }

    public override async Task<MuhasebeHesapPlaniDto> AddAsync(MuhasebeHesapPlaniDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<MuhasebeHesapPlaniDto> UpdateAsync(MuhasebeHesapPlaniDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap plani id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync();
        return items
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .Select(x => Mapper.Map<MuhasebeHesapPlaniDto>(x))
            .ToList();
    }

    private async Task NormalizeAndValidateAsync(MuhasebeHesapPlaniDto dto, int? currentId)
    {
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
        dto.TamKod = (dto.TamKod ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.TamKod))
        {
            throw new BaseException("Tam kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (dto.SeviyeNo <= 0)
        {
            throw new BaseException("Seviye no 0'dan buyuk olmalidir.", 400);
        }

        if (dto.UstHesapId.HasValue)
        {
            if (currentId.HasValue && dto.UstHesapId.Value == currentId.Value)
            {
                throw new BaseException("Bir hesap kendisinin ust hesabi olamaz.", 400);
            }

            var parentExists = await _repository.AnyAsync(x => x.Id == dto.UstHesapId.Value);
            if (!parentExists)
            {
                throw new BaseException("Secilen ust hesap bulunamadi.", 400);
            }
        }

        var tamKodExists = await _repository.AnyAsync(x =>
            x.TamKod == dto.TamKod && (!currentId.HasValue || x.Id != currentId.Value));
        if (tamKodExists)
        {
            throw new BaseException("Tam kod benzersiz olmalidir.", 400);
        }

        var siblingKodExists = await _repository.AnyAsync(x =>
            x.Kod == dto.Kod
            && x.UstHesapId == dto.UstHesapId
            && (!currentId.HasValue || x.Id != currentId.Value));
        if (siblingKodExists)
        {
            throw new BaseException("Ayni ust hesap altinda kod benzersiz olmalidir.", 400);
        }
    }
}

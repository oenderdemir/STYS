using AutoMapper;
using STYS.Binalar.Repositories;
using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;
using STYS.IsletmeAlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.IsletmeAlanlari.Services;

public class IsletmeAlaniService : BaseRdbmsService<IsletmeAlaniDto, IsletmeAlani, int>, IIsletmeAlaniService
{
    private readonly IIsletmeAlaniRepository _isletmeAlaniRepository;
    private readonly IBinaRepository _binaRepository;

    public IsletmeAlaniService(IIsletmeAlaniRepository isletmeAlaniRepository, IBinaRepository binaRepository, IMapper mapper)
        : base(isletmeAlaniRepository, mapper)
    {
        _isletmeAlaniRepository = isletmeAlaniRepository;
        _binaRepository = binaRepository;
    }

    public override async Task<IsletmeAlaniDto> AddAsync(IsletmeAlaniDto dto)
    {
        Normalize(dto);
        await EnsureBinaExistsAsync(dto.BinaId);
        await EnsureUniqueActiveNameAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<IsletmeAlaniDto> UpdateAsync(IsletmeAlaniDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Isletme alani id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureBinaExistsAsync(dto.BinaId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureBinaExistsAsync(int binaId)
    {
        var bina = await _binaRepository.GetByIdAsync(binaId);
        if (bina is null)
        {
            throw new BaseException("Secilen bina bulunamadi.", 400);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(IsletmeAlaniDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _isletmeAlaniRepository.AnyAsync(x =>
            x.AktifMi &&
            x.BinaId == dto.BinaId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni bina altinda ayni isimde aktif isletme alani zaten mevcut.", 400);
        }
    }

    private static void Normalize(IsletmeAlaniDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Isletme alani adi zorunludur.", 400);
        }

        if (dto.BinaId <= 0)
        {
            throw new BaseException("Bina secimi zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }
}
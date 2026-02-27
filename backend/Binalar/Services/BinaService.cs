using AutoMapper;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;
using STYS.Binalar.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Binalar.Services;

public class BinaService : BaseRdbmsService<BinaDto, Bina, int>, IBinaService
{
    private readonly IBinaRepository _binaRepository;
    private readonly ITesisRepository _tesisRepository;

    public BinaService(IBinaRepository binaRepository, ITesisRepository tesisRepository, IMapper mapper)
        : base(binaRepository, mapper)
    {
        _binaRepository = binaRepository;
        _tesisRepository = tesisRepository;
    }

    public override async Task<BinaDto> AddAsync(BinaDto dto)
    {
        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueActiveNameAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<BinaDto> UpdateAsync(BinaDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Bina id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureTesisRulesAsync(int tesisId)
    {
        var tesis = await _tesisRepository.GetByIdAsync(tesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 400);
        }

        if (!tesis.AktifMi)
        {
            throw new BaseException("Pasif tesis altinda bina olusturulamaz veya guncellenemez.", 400);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(BinaDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _binaRepository.AnyAsync(x =>
            x.AktifMi &&
            x.TesisId == dto.TesisId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni isimde aktif bina zaten mevcut.", 400);
        }
    }

    private static void Normalize(BinaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Bina adi zorunludur.", 400);
        }

        if (dto.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (dto.KatSayisi <= 0)
        {
            throw new BaseException("Kat sayisi sifirdan buyuk olmalidir.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }
}
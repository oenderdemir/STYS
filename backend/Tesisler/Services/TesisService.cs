using AutoMapper;
using STYS.Iller.Repositories;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tesisler.Services;

public class TesisService : BaseRdbmsService<TesisDto, Tesis, int>, ITesisService
{
    private readonly ITesisRepository _tesisRepository;
    private readonly IIlRepository _ilRepository;

    public TesisService(ITesisRepository tesisRepository, IIlRepository ilRepository, IMapper mapper)
        : base(tesisRepository, mapper)
    {
        _tesisRepository = tesisRepository;
        _ilRepository = ilRepository;
    }

    public override async Task<TesisDto> AddAsync(TesisDto dto)
    {
        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<TesisDto> UpdateAsync(TesisDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tesis id zorunludur.", 400);
        }

        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task EnsureIlRulesAsync(int ilId)
    {
        var il = await _ilRepository.GetByIdAsync(ilId);
        if (il is null)
        {
            throw new BaseException("Secilen il bulunamadi.", 400);
        }

        if (!il.AktifMi)
        {
            throw new BaseException("Pasif il altinda tesis olusturulamaz veya guncellenemez.", 400);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(TesisDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _tesisRepository.AnyAsync(x =>
            x.AktifMi &&
            x.IlId == dto.IlId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni il altinda ayni isimde aktif tesis zaten mevcut.", 400);
        }
    }

    private static void Normalize(TesisDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Tesis adi zorunludur.", 400);
        }

        if (dto.IlId <= 0)
        {
            throw new BaseException("Il secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Telefon))
        {
            throw new BaseException("Telefon zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Adres))
        {
            throw new BaseException("Adres zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
        dto.Telefon = dto.Telefon.Trim();
        dto.Adres = dto.Adres.Trim();
        dto.Eposta = string.IsNullOrWhiteSpace(dto.Eposta) ? null : dto.Eposta.Trim();
    }
}
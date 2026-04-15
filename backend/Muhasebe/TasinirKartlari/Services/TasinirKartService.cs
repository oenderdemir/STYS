using AutoMapper;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKodlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TasinirKartlari.Services;

public class TasinirKartService : BaseRdbmsService<TasinirKartDto, TasinirKart, int>, ITasinirKartService
{
    private readonly ITasinirKartRepository _repository;
    private readonly ITasinirKodRepository _tasinirKodRepository;

    public TasinirKartService(ITasinirKartRepository repository, ITasinirKodRepository tasinirKodRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _tasinirKodRepository = tasinirKodRepository;
    }

    public override async Task<TasinirKartDto> AddAsync(TasinirKartDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<TasinirKartDto> UpdateAsync(TasinirKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tasinir kart id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id);
        return await base.UpdateAsync(dto);
    }

    private async Task NormalizeAndValidateAsync(TasinirKartDto dto, int? currentId)
    {
        dto.StokKodu = dto.StokKodu?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Birim = dto.Birim?.Trim() ?? "Adet";
        dto.MalzemeTipi = dto.MalzemeTipi?.Trim() ?? string.Empty;
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (dto.TasinirKodId <= 0 || !await _tasinirKodRepository.AnyAsync(x => x.Id == dto.TasinirKodId))
        {
            throw new BaseException("Gecerli bir tasinir kod secilmelidir.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.StokKodu))
        {
            throw new BaseException("Stok kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (!MalzemeTipleri.Hepsi.Contains(dto.MalzemeTipi))
        {
            throw new BaseException("Malzeme tipi gecersiz.", 400);
        }

        if (dto.KdvOrani < 0 || dto.KdvOrani > 100)
        {
            throw new BaseException("KDV orani 0 ile 100 arasinda olmalidir.", 400);
        }

        var duplicate = await _repository.AnyAsync(x => x.StokKodu == dto.StokKodu && (!currentId.HasValue || x.Id != currentId.Value));
        if (duplicate)
        {
            throw new BaseException("Stok kodu benzersiz olmalidir.", 400);
        }
    }
}

using AutoMapper;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;

public class TasinirKodMuhasebeHesapEslemeService
    : BaseRdbmsService<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme, int>,
      ITasinirKodMuhasebeHesapEslemeService
{
    private readonly ITasinirKodMuhasebeHesapEslemeRepository _repository;

    public TasinirKodMuhasebeHesapEslemeService(
        ITasinirKodMuhasebeHesapEslemeRepository repository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
    }

    public async Task<List<TasinirKodMuhasebeHesapEslemeDto>> GetByTasinirKodIdAsync(int tasinirKodId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetByTasinirKodIdAsync(tasinirKodId, cancellationToken);
        return Mapper.Map<List<TasinirKodMuhasebeHesapEslemeDto>>(items);
    }

    public async Task<TasinirKodMuhasebeHesapEslemeDto?> GetVarsayilanAsync(int tasinirKodId, string malzemeTipi, string hareketTipi, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetVarsayilanAsync(tasinirKodId, malzemeTipi, hareketTipi, cancellationToken);
        return Mapper.Map<TasinirKodMuhasebeHesapEslemeDto?>(entity);
    }

    public override async Task<TasinirKodMuhasebeHesapEslemeDto> AddAsync(TasinirKodMuhasebeHesapEslemeDto dto)
    {
        // Geriye donuk uyumluluk: IslemTuru <-> HareketTipi disinda kalan alani doldur
        NormalizeIslemHareketTipleri(dto);

        await ValidateAsync(dto);

        return await base.AddAsync(dto);
    }

    public override async Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto)
    {
        NormalizeIslemHareketTipleri(dto);

        await ValidateAsync(dto);

        return await base.UpdateAsync(dto);
    }

    private static void NormalizeIslemHareketTipleri(TasinirKodMuhasebeHesapEslemeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HareketTipi) && !string.IsNullOrWhiteSpace(dto.IslemTuru))
        {
            dto.HareketTipi = dto.IslemTuru;
        }
        else if (string.IsNullOrWhiteSpace(dto.IslemTuru) && !string.IsNullOrWhiteSpace(dto.HareketTipi))
        {
            dto.IslemTuru = dto.HareketTipi;
        }
    }

    private async Task ValidateAsync(TasinirKodMuhasebeHesapEslemeDto dto)
    {
        if (dto.TasinirKodId <= 0)
            throw new InvalidOperationException("TasinirKodId 0'dan buyuk olmalidir.");

        if (dto.MuhasebeHesapPlaniId <= 0)
            throw new InvalidOperationException("MuhasebeHesapPlaniId 0'dan buyuk olmalidir.");

        if (string.IsNullOrWhiteSpace(dto.MalzemeTipi))
            throw new InvalidOperationException("MalzemeTipi bos olamaz.");

        if (string.IsNullOrWhiteSpace(dto.HareketTipi))
            throw new InvalidOperationException("HareketTipi bos olamaz.");

        // VarsayilanMi = true ise ayni TasinirKodId + MalzemeTipi + HareketTipi icin baska aktif varsayilan kayit olmamali
        if (dto.VarsayilanMi)
        {
            var existing = await _repository.GetVarsayilanAsync(dto.TasinirKodId, dto.MalzemeTipi, dto.HareketTipi);
            if (existing != null && existing.Id != dto.Id)
                throw new InvalidOperationException(
                    $"Bu TasinirKod ({dto.TasinirKodId}), MalzemeTipi ({dto.MalzemeTipi}) ve HareketTipi ({dto.HareketTipi}) icin zaten aktif bir varsayilan esleme mevcut.");
        }
    }
}

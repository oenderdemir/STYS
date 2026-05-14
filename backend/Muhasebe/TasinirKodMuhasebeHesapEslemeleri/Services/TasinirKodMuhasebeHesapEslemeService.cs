using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Entities;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;

public class TasinirKodMuhasebeHesapEslemeService
    : BaseRdbmsService<TasinirKodMuhasebeHesapEslemeDto, TasinirKodMuhasebeHesapEsleme, int>,
      ITasinirKodMuhasebeHesapEslemeService
{
    private readonly ITasinirKodMuhasebeHesapEslemeRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public TasinirKodMuhasebeHesapEslemeService(
        ITasinirKodMuhasebeHesapEslemeRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
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

        await ValidateAsync(dto, CancellationToken.None);

        return await base.AddAsync(dto);
    }

    public override async Task<TasinirKodMuhasebeHesapEslemeDto> UpdateAsync(TasinirKodMuhasebeHesapEslemeDto dto)
    {
        NormalizeIslemHareketTipleri(dto);

        await ValidateAsync(dto, CancellationToken.None);

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

    private async Task ValidateAsync(TasinirKodMuhasebeHesapEslemeDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.TasinirKodId <= 0)
            throw new BaseException("Taşınır kod id 0'dan büyük olmalıdır.", 400);

        if (dto.MuhasebeHesapPlaniId <= 0)
            throw new BaseException("Muhasebe hesap planı id 0'dan büyük olmalıdır.", 400);

        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.MuhasebeHesapPlaniId, cancellationToken);

        if (hesap is null)
            throw new BaseException("Seçilen muhasebe hesabı bulunamadı.", 400);

        if (hesap.IsDeleted)
            throw new BaseException("Seçilen muhasebe hesabı silinmiştir.", 400);

        if (!hesap.AktifMi)
            throw new BaseException("Seçilen muhasebe hesabı aktif değildir.", 400);

        if (hesap.TesisId.HasValue)
            throw new BaseException("Taşınır kod muhasebe eşlemesi için tesis bağımsız ana hesap seçilmelidir.", 400);

        if (hesap.DetayHesapMi)
            throw new BaseException("Taşınır kod muhasebe eşlemesi için detay hesap değil, ana hesap seçilmelidir.", 400);

        if (hesap.HareketGorebilirMi)
            throw new BaseException("Taşınır kod muhasebe eşlemesi için hareket görebilir detay hesap seçilemez.", 400);

        if (string.IsNullOrWhiteSpace(dto.MalzemeTipi))
            throw new BaseException("Malzeme tipi boş olamaz.", 400);

        if (string.IsNullOrWhiteSpace(dto.HareketTipi))
            throw new BaseException("Hareket tipi boş olamaz.", 400);

        // Pasif bir eşleme varsayılan olarak işaretlenemez
        if (dto.VarsayilanMi && !dto.AktifMi)
            throw new BaseException("Pasif bir eşleme varsayılan olarak işaretlenemez.", 400);

        // VarsayilanMi = true ise ayni TasinirKodId + MalzemeTipi + HareketTipi icin baska aktif varsayilan kayit olmamali
        if (dto.VarsayilanMi)
        {
            var existing = await _repository.GetVarsayilanAsync(dto.TasinirKodId, dto.MalzemeTipi, dto.HareketTipi, cancellationToken);
            if (existing != null && existing.Id != dto.Id)
                throw new BaseException(
                    $"Bu taşınır kod ({dto.TasinirKodId}), malzeme tipi ({dto.MalzemeTipi}) ve hareket tipi ({dto.HareketTipi}) için zaten aktif bir varsayılan eşleme mevcut.", 400);
        }
    }
}

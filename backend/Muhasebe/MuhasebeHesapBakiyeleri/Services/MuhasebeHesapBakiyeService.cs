using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Entities;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;

public class MuhasebeHesapBakiyeService
    : BaseRdbmsService<MuhasebeHesapBakiyeDto, MuhasebeHesapBakiye, int>,
      IMuhasebeHesapBakiyeService
{
    private readonly IMuhasebeHesapBakiyeRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public MuhasebeHesapBakiyeService(
        IMuhasebeHesapBakiyeRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<List<MuhasebeHesapBakiyeDto>> GetFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        var entities = await _repository.GetFilteredAsync(filter, cancellationToken);
        return Mapper.Map<List<MuhasebeHesapBakiyeDto>>(entities);
    }

    public async Task<int> CountFilteredAsync(
        MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalize();
        return await _repository.CountFilteredAsync(filter, cancellationToken);
    }

    public async Task<List<MuhasebeHesapBakiyeDto>> GetByTesisYilDonemAsync(
        int tesisId,
        int maliYil,
        int donem,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByTesisYilDonemAsync(tesisId, maliYil, donem, cancellationToken);
        return Mapper.Map<List<MuhasebeHesapBakiyeDto>>(entities);
    }

    public override async Task<MuhasebeHesapBakiyeDto> AddAsync(MuhasebeHesapBakiyeDto dto)
    {
        await ValidateAsync(dto, CancellationToken.None);
        NormalizeAndSetComputedFields(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<MuhasebeHesapBakiyeDto> UpdateAsync(MuhasebeHesapBakiyeDto dto)
    {
        // Mevcut kaydı bul
        var existing = await _dbContext.MuhasebeHesapBakiyeleri
            .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

        if (existing is null)
            throw new BaseException("Güncellenecek hesap bakiyesi bulunamadı.", 404);

        await ValidateAsync(dto, CancellationToken.None, existingId: dto.Id);
        NormalizeAndSetComputedFields(dto);
        return await base.UpdateAsync(dto);
    }

    private static void NormalizeAndSetComputedFields(MuhasebeHesapBakiyeDto dto)
    {
        // Bakiye alanları hesapla
        var net = dto.BorcToplam - dto.AlacakToplam;
        dto.BorcBakiye = net > 0 ? net : 0;
        dto.AlacakBakiye = net < 0 ? Math.Abs(net) : 0;

        // Son güncelleme tarihi
        dto.SonGuncellemeTarihi = DateTime.UtcNow;
    }

    private async Task ValidateAsync(MuhasebeHesapBakiyeDto dto, CancellationToken cancellationToken, int? existingId = null)
    {
        if (dto.TesisId <= 0)
            throw new BaseException("Tesis seçilmelidir.", 400);

        if (dto.MaliYil < 2000 || dto.MaliYil > 2100)
            throw new BaseException("Mali yıl 2000-2100 aralığında olmalıdır.", 400);

        if (dto.Donem < 1 || dto.Donem > 12)
            throw new BaseException("Dönem 1-12 aralığında olmalıdır.", 400);

        if (dto.MuhasebeHesapPlaniId <= 0)
            throw new BaseException("Muhasebe hesap planı seçilmelidir.", 400);

        if (dto.BorcToplam < 0)
            throw new BaseException("Borç toplamı negatif olamaz.", 400);

        if (dto.AlacakToplam < 0)
            throw new BaseException("Alacak toplamı negatif olamaz.", 400);

        // Tesis kontrolü
        var tesis = await _dbContext.Tesisler
            .FirstOrDefaultAsync(x => x.Id == dto.TesisId && !x.IsDeleted, cancellationToken);

        if (tesis is null)
            throw new BaseException("Seçilen tesis bulunamadı.", 400);

        // Muhasebe hesap planı kontrolü ve HesapKodu/HesapAdi set etme
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.MuhasebeHesapPlaniId, cancellationToken);

        if (hesap is null)
            throw new BaseException("Seçilen muhasebe hesabı bulunamadı.", 400);

        if (hesap.IsDeleted)
            throw new BaseException("Seçilen muhasebe hesabı silinmiştir.", 400);

        if (!hesap.AktifMi)
            throw new BaseException("Seçilen muhasebe hesabı aktif değildir.", 400);

        // HesapKodu ve HesapAdi entity'deki TamKod ve Ad'dan set edilir
        dto.HesapKodu = hesap.TamKod;
        dto.HesapAdi = hesap.Ad;

        // Duplicate aktif kayıt kontrolü
        var duplicateQuery = _dbContext.MuhasebeHesapBakiyeleri
            .Where(x =>
                x.TesisId == dto.TesisId
                && x.MaliYil == dto.MaliYil
                && x.Donem == dto.Donem
                && x.MuhasebeHesapPlaniId == dto.MuhasebeHesapPlaniId
                && x.KonsolideMi == dto.KonsolideMi
                && !x.IsDeleted);

        if (existingId.HasValue)
            duplicateQuery = duplicateQuery.Where(x => x.Id != existingId.Value);

        var duplicateExists = await duplicateQuery.AnyAsync(cancellationToken);
        if (duplicateExists)
            throw new BaseException("Aynı tesis, mali yıl, dönem, hesap ve konsolide bilgisi için aktif kayıt zaten mevcut.", 400);
    }
}

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Dtos;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Services;

public class MuhasebeVergiHesapEslemeService
    : BaseRdbmsService<MuhasebeVergiHesapEslemeDto, MuhasebeVergiHesapEsleme, int>,
      IMuhasebeVergiHesapEslemeService
{
    private readonly IMuhasebeVergiHesapEslemeRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public MuhasebeVergiHesapEslemeService(
        IMuhasebeVergiHesapEslemeRepository repository,
        IMapper mapper,
        StysAppDbContext dbContext)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<MuhasebeVergiHesapEslemeDto?> GetAktifEslemeAsync(
        string vergiTipi,
        decimal oran,
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetAktifEslemeAsync(vergiTipi, oran, tesisId, cancellationToken);
        return Mapper.Map<MuhasebeVergiHesapEslemeDto?>(entity);
    }

    public override async Task<IEnumerable<MuhasebeVergiHesapEslemeDto>> GetAllAsync(Func<IQueryable<MuhasebeVergiHesapEsleme>, IQueryable<MuhasebeVergiHesapEsleme>>? include = null)
    {
        var effectiveInclude = include ?? (q => q
            .Include(x => x.AlisKdvHesap)
            .Include(x => x.SatisKdvHesap)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.VergiTipi)
            .ThenBy(x => x.Oran)
            .ThenBy(x => x.TesisId));
        return await base.GetAllAsync(effectiveInclude);
    }

    public override async Task<MuhasebeVergiHesapEslemeDto?> GetByIdAsync(int id, Func<IQueryable<MuhasebeVergiHesapEsleme>, IQueryable<MuhasebeVergiHesapEsleme>>? include = null)
    {
        var effectiveInclude = include ?? (q => q
            .Include(x => x.AlisKdvHesap)
            .Include(x => x.SatisKdvHesap)
            .Where(x => !x.IsDeleted));
        return await base.GetByIdAsync(id, effectiveInclude);
    }

    public override async Task<MuhasebeVergiHesapEslemeDto> AddAsync(MuhasebeVergiHesapEslemeDto dto)
    {
        NormalizeDto(dto);
        await ValidateAsync(dto, CancellationToken.None);
        return await base.AddAsync(dto);
    }

    public override async Task<MuhasebeVergiHesapEslemeDto> UpdateAsync(MuhasebeVergiHesapEslemeDto dto)
    {
        NormalizeDto(dto);
        await ValidateAsync(dto, CancellationToken.None, dto.Id);
        return await base.UpdateAsync(dto);
    }

    private static void NormalizeDto(MuhasebeVergiHesapEslemeDto dto)
    {
        dto.VergiTipi = dto.VergiTipi?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Oran = Math.Round(dto.Oran, 2);
    }

    private async Task ValidateAsync(MuhasebeVergiHesapEslemeDto dto, CancellationToken cancellationToken, int? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(dto.VergiTipi))
            throw new BaseException("Vergi tipi boş olamaz.", 400);

        if (!VergiTipleri.Hepsi.Contains(dto.VergiTipi))
            throw new BaseException($"Desteklenmeyen vergi tipi: {dto.VergiTipi}. Desteklenenler: {string.Join(", ", VergiTipleri.Hepsi)}", 400);

        // Duplicate aktif kayıt kontrolü
        var duplicateQuery = _dbContext.MuhasebeVergiHesapEslemeleri
            .Where(x => x.VergiTipi == dto.VergiTipi
                && x.Oran == dto.Oran
                && x.AktifMi
                && !x.IsDeleted);

        if (dto.TesisId.HasValue)
            duplicateQuery = duplicateQuery.Where(x => x.TesisId == dto.TesisId.Value);
        else
            duplicateQuery = duplicateQuery.Where(x => x.TesisId == null);

        if (existingId.HasValue)
            duplicateQuery = duplicateQuery.Where(x => x.Id != existingId.Value);

        var duplicateExists = await duplicateQuery.AnyAsync(cancellationToken);
        if (duplicateExists)
            throw new BaseException("Aynı tesis, vergi tipi ve oran için aktif vergi hesap eşlemesi zaten mevcut.", 400);

        if (dto.Oran < 0 || dto.Oran > 100)
            throw new BaseException("Vergi oranı 0 ile 100 arasında olmalıdır.", 400);

        if (dto.AlisKdvHesapId <= 0)
            throw new BaseException("Alış KDV hesap id 0'dan büyük olmalıdır.", 400);

        if (dto.SatisKdvHesapId <= 0)
            throw new BaseException("Satış KDV hesap id 0'dan büyük olmalıdır.", 400);

        // Alış KDV hesabı validasyonu
        var alisHesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.AlisKdvHesapId, cancellationToken);

        if (alisHesap is null)
            throw new BaseException("Seçilen alış KDV hesabı bulunamadı.", 400);

        if (alisHesap.IsDeleted)
            throw new BaseException("Seçilen alış KDV hesabı silinmiştir.", 400);

        if (!alisHesap.AktifMi)
            throw new BaseException("Seçilen alış KDV hesabı aktif değildir.", 400);

        await ValidateKdvHesapAsync(alisHesap, "Alış", cancellationToken);

        // Satış KDV hesabı validasyonu
        var satisHesap = await _dbContext.MuhasebeHesapPlanlari
            .FirstOrDefaultAsync(x => x.Id == dto.SatisKdvHesapId, cancellationToken);

        if (satisHesap is null)
            throw new BaseException("Seçilen satış KDV hesabı bulunamadı.", 400);

        if (satisHesap.IsDeleted)
            throw new BaseException("Seçilen satış KDV hesabı silinmiştir.", 400);

        if (!satisHesap.AktifMi)
            throw new BaseException("Seçilen satış KDV hesabı aktif değildir.", 400);

        await ValidateKdvHesapAsync(satisHesap, "Satış", cancellationToken);
    }

    private static Task ValidateKdvHesapAsync(MuhasebeHesapPlanlari.Entities.MuhasebeHesapPlani hesap, string hesapAdi, CancellationToken cancellationToken)
    {
        if (hesap.TesisId.HasValue)
            throw new BaseException($"{hesapAdi} KDV hesap eşlemesi için tesis bağımsız ana hesap seçilmelidir.", 400);

        if (hesap.DetayHesapMi)
            throw new BaseException($"{hesapAdi} KDV hesap eşlemesi için detay hesap değil ana hesap seçilmelidir.", 400);

        if (hesap.HareketGorebilirMi)
            throw new BaseException($"{hesapAdi} KDV hesap eşlemesi için hareket görebilir detay hesap seçilemez.", 400);

        return Task.CompletedTask;
    }
}

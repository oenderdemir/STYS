using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariKartlar.Services;

public class CariKartService : BaseRdbmsService<CariKartDto, CariKart, int>, ICariKartService
{
    private readonly ICariKartRepository _repository;
    private readonly StysAppDbContext _dbContext;

    public CariKartService(ICariKartRepository repository, StysAppDbContext dbContext, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
    }

    public async Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var cari = await _repository.GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        var hareketler = await _dbContext.CariHareketler
            .Where(x => x.CariKartId == cariKartId && x.Durum == CariHareketDurumlari.Aktif)
            .ToListAsync(cancellationToken);

        var toplamBorc = hareketler.Sum(x => x.BorcTutari);
        var toplamAlacak = hareketler.Sum(x => x.AlacakTutari);
        return new CariBakiyeDto
        {
            CariKartId = cari.Id,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Bakiye = toplamBorc - toplamAlacak,
            ParaBirimi = hareketler.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };
    }

    public override async Task<CariKartDto> AddAsync(CariKartDto dto)
    {
        Normalize(dto);
        var normalizedCode = dto.CariKodu.Trim().ToUpperInvariant();
        var exists = await _repository.AnyAsync(x => x.CariKodu.ToUpper() == normalizedCode);
        if (exists)
        {
            throw new BaseException("Cari kodu benzersiz olmalidir.", 400);
        }

        dto.CariKodu = normalizedCode;
        dto.UnvanAdSoyad = dto.UnvanAdSoyad.Trim();
        dto.CariTipi = dto.CariTipi.Trim();
        return await base.AddAsync(dto);
    }

    public override async Task<CariKartDto> UpdateAsync(CariKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari kart id zorunludur.", 400);
        }

        Normalize(dto);
        var normalizedCode = dto.CariKodu.Trim().ToUpperInvariant();
        var exists = await _repository.AnyAsync(x => x.Id != dto.Id.Value && x.CariKodu.ToUpper() == normalizedCode);
        if (exists)
        {
            throw new BaseException("Cari kodu benzersiz olmalidir.", 400);
        }

        dto.CariKodu = normalizedCode;
        dto.UnvanAdSoyad = dto.UnvanAdSoyad.Trim();
        dto.CariTipi = dto.CariTipi.Trim();
        return await base.UpdateAsync(dto);
    }

    private static void Normalize(CariKartDto dto)
    {
        if (!CariKartTipleri.Hepsi.Contains(dto.CariTipi))
        {
            throw new BaseException("Cari tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.CariKodu))
        {
            throw new BaseException("Cari kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.UnvanAdSoyad))
        {
            throw new BaseException("Unvan/Ad Soyad zorunludur.", 400);
        }
    }
}

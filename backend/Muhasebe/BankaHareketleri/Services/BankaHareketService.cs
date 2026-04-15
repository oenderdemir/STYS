using AutoMapper;
using STYS.Muhasebe.BankaHareketleri.Dtos;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.BankaHareketleri.Repositories;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.KasaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.BankaHareketleri.Services;

public class BankaHareketService : BaseRdbmsService<BankaHareketDto, BankaHareket, int>, IBankaHareketService
{
    private readonly ICariKartRepository _cariKartRepository;

    public BankaHareketService(IBankaHareketRepository repository, ICariKartRepository cariKartRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _cariKartRepository = cariKartRepository;
    }

    public override async Task<BankaHareketDto> AddAsync(BankaHareketDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.HareketTipi, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<BankaHareketDto> UpdateAsync(BankaHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Banka hareketi id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.HareketTipi, dto.Durum);
        return await base.UpdateAsync(dto);
    }

    private async Task ValidateAsync(int? cariKartId, string hareketTipi, string durum)
    {
        if (string.IsNullOrWhiteSpace(hareketTipi) || !new[] { KasaHareketTipleri.Tahsilat, KasaHareketTipleri.Odeme, KasaHareketTipleri.Devir, KasaHareketTipleri.Duzeltme }.Contains(hareketTipi))
        {
            throw new BaseException("Hareket tipi gecersiz.", 400);
        }

        if (durum != CariHareketDurumlari.Aktif && durum != CariHareketDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }

        if (cariKartId.HasValue && cariKartId.Value > 0)
        {
            var exists = await _cariKartRepository.AnyAsync(x => x.Id == cariKartId.Value);
            if (!exists)
            {
                throw new BaseException("Cari kart bulunamadi.", 400);
            }
        }
    }
}

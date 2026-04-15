using AutoMapper;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.KasaHareketleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KasaHareketleri.Services;

public class KasaHareketService : BaseRdbmsService<KasaHareketDto, KasaHareket, int>, IKasaHareketService
{
    private readonly ICariKartRepository _cariKartRepository;

    public KasaHareketService(IKasaHareketRepository repository, ICariKartRepository cariKartRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _cariKartRepository = cariKartRepository;
    }

    public override async Task<KasaHareketDto> AddAsync(KasaHareketDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.HareketTipi, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<KasaHareketDto> UpdateAsync(KasaHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kasa hareketi id zorunludur.", 400);
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

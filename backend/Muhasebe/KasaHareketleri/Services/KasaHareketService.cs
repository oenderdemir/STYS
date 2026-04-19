using AutoMapper;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
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
    private readonly IKasaBankaHesapRepository _kasaBankaHesapRepository;

    public KasaHareketService(IKasaHareketRepository repository, ICariKartRepository cariKartRepository, IKasaBankaHesapRepository kasaBankaHesapRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _cariKartRepository = cariKartRepository;
        _kasaBankaHesapRepository = kasaBankaHesapRepository;
    }

    public override async Task<KasaHareketDto> AddAsync(KasaHareketDto dto)
    {
        await ValidateAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<KasaHareketDto> UpdateAsync(KasaHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kasa hareketi id zorunludur.", 400);
        }

        await ValidateAsync(dto);
        return await base.UpdateAsync(dto);
    }

    private async Task ValidateAsync(KasaHareketDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HareketTipi) || !new[] { KasaHareketTipleri.Tahsilat, KasaHareketTipleri.Odeme, KasaHareketTipleri.Devir, KasaHareketTipleri.Duzeltme }.Contains(dto.HareketTipi))
        {
            throw new BaseException("Hareket tipi gecersiz.", 400);
        }

        if (dto.Durum != CariHareketDurumlari.Aktif && dto.Durum != CariHareketDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }

        if (dto.CariKartId.HasValue && dto.CariKartId.Value > 0)
        {
            var exists = await _cariKartRepository.AnyAsync(x => x.Id == dto.CariKartId.Value);
            if (!exists)
            {
                throw new BaseException("Cari kart bulunamadi.", 400);
            }
        }

        if (dto.KasaBankaHesapId.HasValue && dto.KasaBankaHesapId.Value > 0)
        {
            var hesap = await _kasaBankaHesapRepository.GetByIdAsync(dto.KasaBankaHesapId.Value);
            if (hesap is null || !hesap.AktifMi)
            {
                throw new BaseException("Secilen kasa hesabi bulunamadi veya pasif.", 400);
            }

            if (hesap.Tip != KasaBankaHesapTipleri.NakitKasa)
            {
                throw new BaseException("Secilen hesap kasa tipinde degil.", 400);
            }

            dto.KasaKodu = hesap.Kod;
        }
        else if (string.IsNullOrWhiteSpace(dto.KasaKodu))
        {
            throw new BaseException("Kasa kodu veya hesap secimi zorunludur.", 400);
        }
    }
}

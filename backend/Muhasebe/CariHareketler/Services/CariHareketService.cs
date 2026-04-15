using AutoMapper;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariKartlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariHareketler.Services;

public class CariHareketService : BaseRdbmsService<CariHareketDto, CariHareket, int>, ICariHareketService
{
    private readonly ICariHareketRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;

    public CariHareketService(ICariHareketRepository repository, ICariKartRepository cariKartRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
    }

    public async Task<CariEkstreDto> GetEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default)
    {
        var cari = await _cariKartRepository.GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        var hareketler = await _repository.GetCariEkstresiAsync(cariKartId, baslangic, bitis, cancellationToken);
        var dtoHareketler = Mapper.Map<List<CariHareketDto>>(hareketler);
        return new CariEkstreDto
        {
            CariKartId = cari.Id,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.BorcTutari),
            ToplamAlacak = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.AlacakTutari),
            Bakiye = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.BorcTutari - x.AlacakTutari),
            Hareketler = dtoHareketler
        };
    }

    public override async Task<CariHareketDto> AddAsync(CariHareketDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<CariHareketDto> UpdateAsync(CariHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari hareket id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.Durum);
        return await base.UpdateAsync(dto);
    }

    private async Task ValidateAsync(int cariKartId, string durum)
    {
        if (cariKartId <= 0)
        {
            throw new BaseException("Cari secimi zorunludur.", 400);
        }

        var cariExists = await _cariKartRepository.AnyAsync(x => x.Id == cariKartId);
        if (!cariExists)
        {
            throw new BaseException("Cari kart bulunamadi.", 400);
        }

        if (durum != CariHareketDurumlari.Aktif && durum != CariHareketDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }
    }
}

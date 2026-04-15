using AutoMapper;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

public class TahsilatOdemeBelgesiService : BaseRdbmsService<TahsilatOdemeBelgesiDto, TahsilatOdemeBelgesi, int>, ITahsilatOdemeBelgesiService
{
    private readonly ITahsilatOdemeBelgesiRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;

    public TahsilatOdemeBelgesiService(
        ITahsilatOdemeBelgesiRepository repository,
        ICariKartRepository cariKartRepository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
    }

    public async Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetGunlukAsync(gun, cancellationToken);
        var aktifler = list.Where(x => x.Durum == TahsilatOdemeBelgeDurumlari.Aktif).ToList();
        var tahsilat = aktifler.Where(x => x.BelgeTipi == TahsilatOdemeBelgeTipleri.Tahsilat).Sum(x => x.Tutar);
        var odeme = aktifler.Where(x => x.BelgeTipi == TahsilatOdemeBelgeTipleri.Odeme).Sum(x => x.Tutar);

        return new TahsilatOdemeOzetDto
        {
            Gun = gun.Date,
            ToplamTahsilat = tahsilat,
            ToplamOdeme = odeme,
            Net = tahsilat - odeme,
            ParaBirimi = aktifler.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };
    }

    public override async Task<TahsilatOdemeBelgesiDto> AddAsync(TahsilatOdemeBelgesiDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.BelgeTipi, dto.OdemeYontemi, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<TahsilatOdemeBelgesiDto> UpdateAsync(TahsilatOdemeBelgesiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tahsilat/odeme belgesi id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.BelgeTipi, dto.OdemeYontemi, dto.Durum);
        return await base.UpdateAsync(dto);
    }

    private async Task ValidateAsync(int cariKartId, string belgeTipi, string odemeYontemi, string durum)
    {
        if (cariKartId <= 0)
        {
            throw new BaseException("Cari kart secimi zorunludur.", 400);
        }

        var cariExists = await _cariKartRepository.AnyAsync(x => x.Id == cariKartId);
        if (!cariExists)
        {
            throw new BaseException("Cari kart bulunamadi.", 400);
        }

        if (belgeTipi != TahsilatOdemeBelgeTipleri.Tahsilat && belgeTipi != TahsilatOdemeBelgeTipleri.Odeme)
        {
            throw new BaseException("Belge tipi gecersiz.", 400);
        }

        if (!OdemeYontemleri.Hepsi.Contains(odemeYontemi))
        {
            throw new BaseException("Odeme yontemi gecersiz.", 400);
        }

        if (durum != TahsilatOdemeBelgeDurumlari.Aktif && durum != TahsilatOdemeBelgeDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }
    }
}

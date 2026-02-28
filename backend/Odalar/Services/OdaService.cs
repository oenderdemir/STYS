using AutoMapper;
using STYS.Binalar.Repositories;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using STYS.Odalar.Repositories;
using STYS.OdaTipleri.Entities;
using STYS.OdaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Odalar.Services;

public class OdaService : BaseRdbmsService<OdaDto, Oda, int>, IOdaService
{
    private readonly IOdaRepository _odaRepository;
    private readonly IBinaRepository _binaRepository;
    private readonly IOdaTipiRepository _odaTipiRepository;

    public OdaService(
        IOdaRepository odaRepository,
        IBinaRepository binaRepository,
        IOdaTipiRepository odaTipiRepository,
        IMapper mapper)
        : base(odaRepository, mapper)
    {
        _odaRepository = odaRepository;
        _binaRepository = binaRepository;
        _odaTipiRepository = odaTipiRepository;
    }

    public override async Task<OdaDto> AddAsync(OdaDto dto)
    {
        Normalize(dto);
        var odaTipi = await EnsureDependenciesAsync(dto);
        ValidateBedCount(dto, odaTipi.Kapasite, odaTipi.PaylasimliMi);
        await EnsureUniqueActiveRoomNoAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<OdaDto> UpdateAsync(OdaDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Oda id zorunludur.", 400);
        }

        Normalize(dto);
        var odaTipi = await EnsureDependenciesAsync(dto);
        ValidateBedCount(dto, odaTipi.Kapasite, odaTipi.PaylasimliMi);
        await EnsureUniqueActiveRoomNoAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    private async Task<OdaTipi> EnsureDependenciesAsync(OdaDto dto)
    {
        var bina = await _binaRepository.GetByIdAsync(dto.BinaId);
        if (bina is null)
        {
            throw new BaseException("Secilen bina bulunamadi.", 400);
        }

        if (!bina.AktifMi)
        {
            throw new BaseException("Pasif bina altinda oda olusturulamaz veya guncellenemez.", 400);
        }

        var odaTipi = await _odaTipiRepository.GetByIdAsync(dto.TesisOdaTipiId);
        if (odaTipi is null)
        {
            throw new BaseException("Secilen tesis oda tipi bulunamadi.", 400);
        }

        if (!odaTipi.AktifMi)
        {
            throw new BaseException("Pasif tesis oda tipi secilemez.", 400);
        }

        if (odaTipi.TesisId != bina.TesisId)
        {
            throw new BaseException("Secilen oda tipi, odanin bulundugu tesis ile uyumlu degil.", 400);
        }

        return odaTipi;
    }

    private async Task EnsureUniqueActiveRoomNoAsync(OdaDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedRoomNo = dto.OdaNo.Trim().ToUpperInvariant();
        var exists = await _odaRepository.AnyAsync(x =>
            x.AktifMi &&
            x.BinaId == dto.BinaId &&
            x.OdaNo.ToUpper() == normalizedRoomNo &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni bina altinda ayni oda numarasina sahip aktif oda zaten mevcut.", 400);
        }
    }

    private static void ValidateBedCount(OdaDto dto, int odaTipiKapasitesi, bool paylasimliMi)
    {
        if (paylasimliMi)
        {
            if (!dto.YatakSayisi.HasValue || dto.YatakSayisi.Value <= 0)
            {
                throw new BaseException("Paylasimli oda icin yatak sayisi zorunludur.", 400);
            }

            if (dto.YatakSayisi.Value > odaTipiKapasitesi)
            {
                throw new BaseException("Yatak sayisi oda tipi kapasitesini asamaz.", 400);
            }

            return;
        }

        if (dto.YatakSayisi.HasValue && dto.YatakSayisi.Value <= 0)
        {
            throw new BaseException("Yatak sayisi girilecekse sifirdan buyuk olmalidir.", 400);
        }
    }

    private static void Normalize(OdaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OdaNo))
        {
            throw new BaseException("Oda numarasi zorunludur.", 400);
        }

        if (dto.BinaId <= 0)
        {
            throw new BaseException("Bina secimi zorunludur.", 400);
        }

        if (dto.TesisOdaTipiId <= 0)
        {
            throw new BaseException("Tesis oda tipi secimi zorunludur.", 400);
        }

        dto.OdaNo = dto.OdaNo.Trim();
    }
}

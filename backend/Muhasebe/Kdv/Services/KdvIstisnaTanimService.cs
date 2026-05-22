using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Kdv.Services;

public class KdvIstisnaTanimService : BaseRdbmsService<KdvIstisnaTanimDto, KdvIstisnaTanim, int>, IKdvIstisnaTanimService
{
    private readonly IKdvIstisnaTanimRepository _repository;
    private readonly IMapper _mapper;

    public KdvIstisnaTanimService(
        IKdvIstisnaTanimRepository repository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<KdvIstisnaTanimDto>> FilterAsync(KdvIstisnaTanimFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _repository.Where(x => true);

        if (!string.IsNullOrWhiteSpace(filter.Kod))
        {
            var kod = filter.Kod.Trim();
            query = query.Where(x => x.Kod.Contains(kod));
        }

        if (!string.IsNullOrWhiteSpace(filter.Ad))
        {
            var ad = filter.Ad.Trim();
            query = query.Where(x => x.Ad.Contains(ad));
        }

        if (filter.UygulamaTipi.HasValue)
            query = query.Where(x => x.UygulamaTipi == filter.UygulamaTipi.Value);

        if (filter.AktifMi.HasValue)
            query = query.Where(x => x.AktifMi == filter.AktifMi.Value);

        if (filter.SatisIslemlerindeKullanilirMi.HasValue)
            query = query.Where(x => x.SatisIslemlerindeKullanilirMi == filter.SatisIslemlerindeKullanilirMi.Value);

        if (filter.AlisIslemlerindeKullanilirMi.HasValue)
            query = query.Where(x => x.AlisIslemlerindeKullanilirMi == filter.AlisIslemlerindeKullanilirMi.Value);

        var entities = await query.OrderBy(x => x.Kod).ToListAsync(cancellationToken);
        return _mapper.Map<List<KdvIstisnaTanimDto>>(entities);
    }

    public override async Task<KdvIstisnaTanimDto> AddAsync(KdvIstisnaTanimDto dto)
    {
        Normalize(dto);
        await ValidateCreateAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<KdvIstisnaTanimDto> UpdateAsync(KdvIstisnaTanimDto dto)
    {
        Normalize(dto);
        var existing = await _repository.GetByIdAsync(dto.Id.GetValueOrDefault());

        if (existing is null)
            throw new BaseException($"Id={dto.Id} KDV istisna tanımı bulunamadı.", 404);

        await ValidateUpdateAsync(dto, existing);
        return await base.UpdateAsync(dto);
    }

    private static void Normalize(KdvIstisnaTanimDto dto)
    {
        dto.Kod = dto.Kod?.Trim() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Aciklama = dto.Aciklama?.Trim();
    }

    private async Task ValidateCreateAsync(KdvIstisnaTanimDto dto)
    {
        ValidateRequiredFields(dto);
        ValidateMaxLengths(dto);
        ValidateUygulamaTipi(dto);
        ValidateKullanimAlani(dto);
        ValidateGecerlilikTarihleri(dto);
        await ValidateKodUniqueAsync(dto.Kod, null);
    }

    private async Task ValidateUpdateAsync(KdvIstisnaTanimDto dto, KdvIstisnaTanim existing)
    {
        ValidateRequiredFields(dto);
        ValidateMaxLengths(dto);
        ValidateUygulamaTipi(dto);
        ValidateKullanimAlani(dto);
        ValidateGecerlilikTarihleri(dto);
        await ValidateKodUniqueAsync(dto.Kod, dto.Id);
    }

    private static void ValidateRequiredFields(KdvIstisnaTanimDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
            throw new BaseException("Kod zorunludur.", 400);

        if (string.IsNullOrWhiteSpace(dto.Ad))
            throw new BaseException("Ad zorunludur.", 400);
    }

    private static void ValidateMaxLengths(KdvIstisnaTanimDto dto)
    {
        if (dto.Kod.Length > 50)
            throw new BaseException("Kod en fazla 50 karakter olabilir.", 400);

        if (dto.Ad.Length > 250)
            throw new BaseException("Ad en fazla 250 karakter olabilir.", 400);

        if (dto.Aciklama?.Length > 1000)
            throw new BaseException("Açıklama en fazla 1000 karakter olabilir.", 400);
    }

    private static void ValidateUygulamaTipi(KdvIstisnaTanimDto dto)
    {
        if (dto.UygulamaTipi == KdvUygulamaTipi.Kdvli)
            throw new BaseException("KDV'li tipi KDV istisna tanımı olarak kaydedilemez.", 400);
    }

    private static void ValidateKullanimAlani(KdvIstisnaTanimDto dto)
    {
        if (!dto.SatisIslemlerindeKullanilirMi && !dto.AlisIslemlerindeKullanilirMi)
            throw new BaseException("En az bir kullanım alanı (Satış veya Alış) seçilmelidir.", 400);
    }

    private static void ValidateGecerlilikTarihleri(KdvIstisnaTanimDto dto)
    {
        if (dto.GecerlilikBaslangicTarihi.HasValue
            && dto.GecerlilikBitisTarihi.HasValue
            && dto.GecerlilikBitisTarihi.Value <= dto.GecerlilikBaslangicTarihi.Value)
        {
            throw new BaseException(
                "Geçerlilik bitiş tarihi, başlangıç tarihinden sonra olmalıdır.", 400);
        }
    }

    // Tam/Kısmi istisna: herhangi bir otomatik varsayılan mantık uygulanmaz.
    // Kullanıcı uygulama tipini manuel olarak seçer.

    private async Task ValidateKodUniqueAsync(string kod, int? excludeId)
    {
        var exists = await _repository.AnyAsync(
            x => x.Kod.ToLower() == kod.ToLower() && (!excludeId.HasValue || x.Id != excludeId.Value));

        if (exists)
            throw new BaseException($"\"{kod}\" kodlu bir KDV istisna tanımı zaten mevcut.", 409);
    }
}

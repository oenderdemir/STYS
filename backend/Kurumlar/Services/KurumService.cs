using AutoMapper;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Entities;
using STYS.Kurumlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kurumlar.Services;

public class KurumService : BaseRdbmsService<KurumDto, Kurum, int>, IKurumService
{
    private readonly IKurumRepository _kurumRepository;

    public KurumService(IKurumRepository kurumRepository, IMapper mapper)
        : base(kurumRepository, mapper)
    {
        _kurumRepository = kurumRepository;
    }

    public async Task<List<KurumDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var kurumlar = await base.GetAllAsync();
        return kurumlar.OrderBy(x => x.Ad).ToList();
    }

    public Task<KurumDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return base.GetByIdAsync(id);
    }

    public async Task<KurumDto> CreateAsync(CreateKurumRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = Mapper.Map<KurumDto>(request);
        return await AddAsync(dto);
    }

    public async Task<KurumDto> UpdateAsync(int id, UpdateKurumRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await GetByIdAsync(id, cancellationToken);
        if (existing is null)
            throw new BaseException("Kurum bulunamadi.", 404);

        var dto = Mapper.Map<KurumDto>(request);
        dto.Id = id;
        dto.LogoDosyaAdi = existing.LogoDosyaAdi;
        dto.LogoOrijinalDosyaAdi = existing.LogoOrijinalDosyaAdi;
        dto.LogoContentType = existing.LogoContentType;
        dto.LogoBoyut = existing.LogoBoyut;
        dto.LogoYuklenmeTarihi = existing.LogoYuklenmeTarihi;
        return await UpdateAsync(dto);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureKurumExistsAsync(id);
        await base.DeleteAsync(id);
    }

    public override async Task<KurumDto> AddAsync(KurumDto dto)
    {
        Normalize(dto);
        await EnsureUniqueKodAsync(dto.Kod);
        return await base.AddAsync(dto);
    }

    public override async Task<KurumDto> UpdateAsync(KurumDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kurum id zorunludur.", 400);
        }

        await EnsureKurumExistsAsync(dto.Id.Value);
        Normalize(dto);
        await EnsureUniqueKodAsync(dto.Kod, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureKurumExistsAsync(id);
        await base.DeleteAsync(id);
    }

    private async Task EnsureUniqueKodAsync(string kod, int? excludedId = null)
    {
        var exists = await _kurumRepository.ExistsByKodAsync(kod, excludedId);
        if (exists)
        {
            throw new BaseException("Ayni kodda kurum zaten mevcut.", 400);
        }
    }

    private async Task EnsureKurumExistsAsync(int id)
    {
        var entity = await _kurumRepository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new BaseException("Kurum bulunamadi.", 404);
        }
    }

    private static void Normalize(KurumDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kurum kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Kurum adi zorunludur.", 400);
        }

        dto.Kod = dto.Kod.Trim();
        dto.Ad = dto.Ad.Trim();
        dto.VergiNo = NormalizeOptional(dto.VergiNo);
        dto.Telefon = NormalizeOptional(dto.Telefon);
        dto.Eposta = NormalizeOptional(dto.Eposta);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

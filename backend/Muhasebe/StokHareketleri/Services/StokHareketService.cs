using AutoMapper;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokHareketleri.Services;

public class StokHareketService : BaseRdbmsService<StokHareketDto, StokHareket, int>, IStokHareketService
{
    private readonly IStokHareketRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly ICariKartRepository _cariKartRepository;

    public StokHareketService(
        IStokHareketRepository repository,
        IDepoRepository depoRepository,
        ITasinirKartRepository tasinirKartRepository,
        ICariKartRepository cariKartRepository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _depoRepository = depoRepository;
        _tasinirKartRepository = tasinirKartRepository;
        _cariKartRepository = cariKartRepository;
    }

    public override async Task<StokHareketDto> AddAsync(StokHareketDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        return await base.AddAsync(dto);
    }

    public override async Task<StokHareketDto> UpdateAsync(StokHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Stok hareket id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? depoId, CancellationToken cancellationToken = default)
        => await _repository.GetDepoStokBakiyeleriAsync(depoId, cancellationToken);

    public async Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? depoId, CancellationToken cancellationToken = default)
        => await _repository.GetStokKartOzetleriAsync(depoId, cancellationToken);

    private async Task NormalizeAndValidateAsync(StokHareketDto dto, int? currentId)
    {
        dto.HareketTipi = dto.HareketTipi?.Trim() ?? string.Empty;
        dto.Durum = dto.Durum?.Trim() ?? string.Empty;
        dto.BelgeNo = NormalizeOptional(dto.BelgeNo);
        dto.Aciklama = NormalizeOptional(dto.Aciklama);
        dto.KaynakModul = NormalizeOptional(dto.KaynakModul);

        if (dto.DepoId <= 0 || !await _depoRepository.AnyAsync(x => x.Id == dto.DepoId))
        {
            throw new BaseException("Gecerli bir depo secilmelidir.", 400);
        }

        if (dto.TasinirKartId <= 0 || !await _tasinirKartRepository.AnyAsync(x => x.Id == dto.TasinirKartId))
        {
            throw new BaseException("Gecerli bir tasinir kart secilmelidir.", 400);
        }

        if (dto.CariKartId.HasValue && dto.CariKartId.Value > 0)
        {
            var cariExists = await _cariKartRepository.AnyAsync(x => x.Id == dto.CariKartId.Value);
            if (!cariExists)
            {
                throw new BaseException("Secilen cari kart bulunamadi.", 400);
            }
        }

        if (!StokHareketTipleri.Hepsi.Contains(dto.HareketTipi))
        {
            throw new BaseException("Hareket tipi gecersiz.", 400);
        }

        if (!StokHareketDurumlari.Hepsi.Contains(dto.Durum))
        {
            throw new BaseException("Durum gecersiz.", 400);
        }

        if (dto.Miktar <= 0)
        {
            throw new BaseException("Miktar 0'dan buyuk olmalidir.", 400);
        }

        if (dto.BirimFiyat < 0)
        {
            throw new BaseException("Birim fiyat negatif olamaz.", 400);
        }

        if (dto.HareketTarihi == default)
        {
            dto.HareketTarihi = DateTime.UtcNow;
        }
    }

    private static decimal CalculateTutar(decimal miktar, decimal birimFiyat)
        => Math.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

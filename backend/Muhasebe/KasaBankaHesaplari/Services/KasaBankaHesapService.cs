using AutoMapper;
using System;
using STYS.Muhasebe.KasaBankaHesaplari.Dtos;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KasaBankaHesaplari.Services;

public class KasaBankaHesapService : BaseRdbmsService<KasaBankaHesapDto, KasaBankaHesap, int>, IKasaBankaHesapService
{
    private readonly IKasaBankaHesapRepository _repository;
    private readonly IMuhasebeHesapPlaniRepository _muhasebeHesapPlaniRepository;

    public KasaBankaHesapService(IKasaBankaHesapRepository repository, IMuhasebeHesapPlaniRepository muhasebeHesapPlaniRepository, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _muhasebeHesapPlaniRepository = muhasebeHesapPlaniRepository;
    }

    public override async Task<KasaBankaHesapDto> AddAsync(KasaBankaHesapDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<KasaBankaHesapDto> UpdateAsync(KasaBankaHesapDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<KasaBankaHesapDto>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var items = await _repository.GetByTipAsync(tip, onlyActive, cancellationToken);
        return items.Select(Mapper.Map<KasaBankaHesapDto>).ToList();
    }

    public async Task<List<MuhasebeHesapSecimDto>> GetMuhasebeHesapSecimleriAsync(string tip, CancellationToken cancellationToken = default)
    {
        if (!KasaBankaHesapTipleri.TumTipler.Contains(tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        var prefix = tip == KasaBankaHesapTipleri.NakitKasa ? "1.10.100" : "1.10.102";
        var matches = await _muhasebeHesapPlaniRepository.GetByTamKodPrefixAsync(prefix, cancellationToken);

        return matches.Select(x => new MuhasebeHesapSecimDto
        {
            Id = x.Id,
            TamKod = x.TamKod,
            Ad = x.Ad
        }).ToList();
    }

    private async Task NormalizeAndValidateAsync(KasaBankaHesapDto dto, int? currentId)
    {
        dto.Tip = (dto.Tip ?? string.Empty).Trim();
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.BankaAdi = NormalizeOptional(dto.BankaAdi, 128);
        dto.SubeAdi = NormalizeOptional(dto.SubeAdi, 128);
        dto.HesapNo = NormalizeOptional(dto.HesapNo, 64);
        dto.Iban = NormalizeOptional(dto.Iban?.Replace(" ", string.Empty).ToUpperInvariant(), 34);
        dto.MusteriNo = NormalizeOptional(dto.MusteriNo, 64);
        dto.HesapTuru = NormalizeOptional(dto.HesapTuru, 32);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        if (!KasaBankaHesapTipleri.TumTipler.Contains(dto.Tip))
        {
            throw new BaseException("Hesap tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        var duplicateKod = await _repository.ExistsByKodAsync(dto.Kod, currentId);
        if (duplicateKod)
        {
            throw new BaseException("Hesap kodu benzersiz olmalidir.", 400);
        }

        var muhasebeHesap = await _muhasebeHesapPlaniRepository.GetByIdAsync(dto.MuhasebeHesapPlaniId);
        if (muhasebeHesap is null)
        {
            throw new BaseException("Muhasebe hesap plani kaydi bulunamadi.", 400);
        }

        if (!muhasebeHesap.AktifMi)
        {
            throw new BaseException("Secilen muhasebe hesabi pasif.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.NakitKasa && !muhasebeHesap.TamKod.StartsWith("1.10.100", StringComparison.Ordinal))
        {
            throw new BaseException("Nakit kasa hesaplari sadece 1.10.100 ile baslayan muhasebe kodlarina baglanabilir.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.Banka && !muhasebeHesap.TamKod.StartsWith("1.10.102", StringComparison.Ordinal))
        {
            throw new BaseException("Banka hesaplari sadece 1.10.102 ile baslayan muhasebe kodlarina baglanabilir.", 400);
        }

        if (dto.Tip == KasaBankaHesapTipleri.Banka)
        {
            if (string.IsNullOrWhiteSpace(dto.BankaAdi))
            {
                throw new BaseException("Banka tipi hesap icin banka adi zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(dto.HesapNo) && string.IsNullOrWhiteSpace(dto.Iban))
            {
                throw new BaseException("Banka tipi hesap icin hesap no veya IBAN zorunludur.", 400);
            }
        }
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }
}

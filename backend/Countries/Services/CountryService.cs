using AutoMapper;
using STYS.Countries.Dto;
using STYS.Countries.Entities;
using STYS.Countries.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Countries.Services;

public class CountryService : BaseRdbmsService<CountryDto, Country>, ICountryService
{
    private readonly ICountryRepository _countryRepository;

    public CountryService(ICountryRepository countryRepository, IMapper mapper)
        : base(countryRepository, mapper)
    {
        _countryRepository = countryRepository;
    }

    public override async Task<CountryDto> AddAsync(CountryDto dto)
    {
        Normalize(dto);
        await EnsureUniqueCodeAsync(dto.Code);
        return await base.AddAsync(dto);
    }

    public override async Task<CountryDto> UpdateAsync(CountryDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Country id is required.");
        }

        Normalize(dto);
        await EnsureUniqueCodeAsync(dto.Code, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<Guid>> AddRangeAsync(IEnumerable<CountryDto> countries)
    {
        var ids = new List<Guid>();
        foreach (var country in countries)
        {
            var added = await AddAsync(country);
            if (added.Id.HasValue)
            {
                ids.Add(added.Id.Value);
            }
        }

        return ids;
    }

    private async Task EnsureUniqueCodeAsync(string code, Guid? excludedId = null)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var exists = await _countryRepository.AnyAsync(x =>
            x.Code == normalizedCode &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Country code already exists.");
        }
    }

    private static void Normalize(CountryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new InvalidOperationException("Country name is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            throw new InvalidOperationException("Country code is required.");
        }

        dto.Name = dto.Name.Trim();
        dto.Code = dto.Code.Trim().ToUpperInvariant();
    }
}

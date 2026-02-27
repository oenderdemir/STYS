using STYS.Countries.Dto;
using STYS.Countries.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace STYS.Countries.Services;

public interface ICountryService : IBaseService<CountryDto, Country>
{
    Task<List<Guid>> AddRangeAsync(IEnumerable<CountryDto> countries);
}

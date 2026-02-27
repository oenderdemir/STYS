using STYS.Countries.Dto;
using STYS.Countries.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Countries.Services;

public interface ICountryService : IBaseRdbmsService<CountryDto, Country>
{
    Task<List<Guid>> AddRangeAsync(IEnumerable<CountryDto> countries);
}

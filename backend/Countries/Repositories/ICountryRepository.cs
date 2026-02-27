using STYS.Countries.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace STYS.Countries.Repositories;

public interface ICountryRepository : IBaseRepository<Country>
{
    Task<Country?> GetByCodeAsync(string code);
}

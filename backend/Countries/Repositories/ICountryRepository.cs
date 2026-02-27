using STYS.Countries.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Countries.Repositories;

public interface ICountryRepository : IBaseRdbmsRepository<Country>
{
    Task<Country?> GetByCodeAsync(string code);
}

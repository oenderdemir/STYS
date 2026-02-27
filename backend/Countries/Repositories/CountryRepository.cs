using AutoMapper;
using STYS.Countries.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace STYS.Countries.Repositories;

public class CountryRepository : BaseRepository<Country>, ICountryRepository
{
    public CountryRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }

    public Task<Country?> GetByCodeAsync(string code)
    {
        return FirstOrDefaultAsync(x => x.Code == code);
    }
}

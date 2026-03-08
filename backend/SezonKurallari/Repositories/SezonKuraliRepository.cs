using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.SezonKurallari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.SezonKurallari.Repositories;

public class SezonKuraliRepository : BaseRdbmsRepository<SezonKurali, int>, ISezonKuraliRepository
{
    public SezonKuraliRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

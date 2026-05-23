using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.SatisBelgeleri.Repositories;

public class SatisBelgesiRepository : BaseRdbmsRepository<SatisBelgesi, int>, ISatisBelgesiRepository
{
    public SatisBelgesiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

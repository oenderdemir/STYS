using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.Depolar.Repositories;

public class DepoRepository : BaseRdbmsRepository<Depo, int>, IDepoRepository
{
    public DepoRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

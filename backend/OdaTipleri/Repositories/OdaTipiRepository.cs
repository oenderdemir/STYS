using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaTipleri.Repositories;

public class OdaTipiRepository : BaseRdbmsRepository<OdaTipi, int>, IOdaTipiRepository
{
    public OdaTipiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
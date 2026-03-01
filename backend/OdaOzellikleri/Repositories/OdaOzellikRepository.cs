using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaOzellikleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaOzellikleri.Repositories;

public class OdaOzellikRepository : BaseRdbmsRepository<OdaOzellik, int>, IOdaOzellikRepository
{
    public OdaOzellikRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

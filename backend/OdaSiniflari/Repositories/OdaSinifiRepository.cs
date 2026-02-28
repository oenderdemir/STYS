using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaSiniflari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaSiniflari.Repositories;

public class OdaSinifiRepository : BaseRdbmsRepository<OdaSinifi, int>, IOdaSinifiRepository
{
    public OdaSinifiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

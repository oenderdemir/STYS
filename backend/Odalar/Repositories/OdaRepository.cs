using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Odalar.Repositories;

public class OdaRepository : BaseRdbmsRepository<Oda, int>, IOdaRepository
{
    public OdaRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
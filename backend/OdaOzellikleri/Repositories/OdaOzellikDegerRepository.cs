using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaOzellikleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaOzellikleri.Repositories;

public class OdaOzellikDegerRepository : BaseRdbmsRepository<OdaOzellikDeger, int>, IOdaOzellikDegerRepository
{
    public OdaOzellikDegerRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

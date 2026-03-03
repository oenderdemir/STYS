using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.MisafirTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.MisafirTipleri.Repositories;

public class MisafirTipiRepository : BaseRdbmsRepository<MisafirTipi, int>, IMisafirTipiRepository
{
    public MisafirTipiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

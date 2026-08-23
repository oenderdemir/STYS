using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokTalepleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokTalepleri.Repositories;

public class StokTalepRepository : BaseRdbmsRepository<StokTalep, int>, IStokTalepRepository
{
    public StokTalepRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Kamp.Repositories;

public class KampDonemiRepository : BaseRdbmsRepository<KampDonemi, int>, IKampDonemiRepository
{
    public KampDonemiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

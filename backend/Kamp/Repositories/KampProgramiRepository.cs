using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Kamp.Repositories;

public class KampProgramiRepository : BaseRdbmsRepository<KampProgrami, int>, IKampProgramiRepository
{
    public KampProgramiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

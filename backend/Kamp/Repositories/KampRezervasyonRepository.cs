using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Kamp.Repositories;

public class KampRezervasyonRepository : BaseRdbmsRepository<KampRezervasyon, int>, IKampRezervasyonRepository
{
    public KampRezervasyonRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

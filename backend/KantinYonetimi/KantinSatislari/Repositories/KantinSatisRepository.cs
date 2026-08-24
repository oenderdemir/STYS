using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.KantinSatislari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.KantinYonetimi.KantinSatislari.Repositories;

public class KantinSatisRepository : BaseRdbmsRepository<KantinSatis, int>, IKantinSatisRepository
{
    public KantinSatisRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

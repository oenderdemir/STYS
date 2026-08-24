using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.Kantinler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.KantinYonetimi.Kantinler.Repositories;

public class KantinRepository : BaseRdbmsRepository<Kantin, int>, IKantinRepository
{
    public KantinRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.Kantinler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.KantinYonetimi.Kantinler.Repositories;

public class KantinUrunRepository : BaseRdbmsRepository<KantinUrun, int>, IKantinUrunRepository
{
    public KantinUrunRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.BankaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.BankaHareketleri.Repositories;

public class BankaHareketRepository : BaseRdbmsRepository<BankaHareket, int>, IBankaHareketRepository
{
    public BankaHareketRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}


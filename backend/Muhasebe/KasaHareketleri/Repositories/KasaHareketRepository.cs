using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.KasaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.KasaHareketleri.Repositories;

public class KasaHareketRepository : BaseRdbmsRepository<KasaHareket, int>, IKasaHareketRepository
{
    public KasaHareketRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}


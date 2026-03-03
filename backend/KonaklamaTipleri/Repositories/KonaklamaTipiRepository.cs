using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.KonaklamaTipleri.Repositories;

public class KonaklamaTipiRepository : BaseRdbmsRepository<KonaklamaTipi, int>, IKonaklamaTipiRepository
{
    public KonaklamaTipiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

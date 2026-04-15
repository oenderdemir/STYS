using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.CariKartlar.Repositories;

public class CariKartRepository : BaseRdbmsRepository<CariKart, int>, ICariKartRepository
{
    public CariKartRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}


using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SarfFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.SarfFisleri.Repositories;

public class SarfFisiRepository : BaseRdbmsRepository<SarfFisi, int>, ISarfFisiRepository
{
    public SarfFisiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.PaketTurleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.PaketTurleri.Repositories;

public class PaketTuruRepository : BaseRdbmsRepository<PaketTuru, int>, IPaketTuruRepository
{
    public PaketTuruRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

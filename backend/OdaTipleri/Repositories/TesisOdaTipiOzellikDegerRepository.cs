using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaTipleri.Repositories;

public class TesisOdaTipiOzellikDegerRepository : BaseRdbmsRepository<TesisOdaTipiOzellikDeger, int>, ITesisOdaTipiOzellikDegerRepository
{
    public TesisOdaTipiOzellikDegerRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

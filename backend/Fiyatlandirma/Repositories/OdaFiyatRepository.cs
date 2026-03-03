using AutoMapper;
using STYS.Fiyatlandirma.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Fiyatlandirma.Repositories;

public class OdaFiyatRepository : BaseRdbmsRepository<OdaFiyat, int>, IOdaFiyatRepository
{
    public OdaFiyatRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

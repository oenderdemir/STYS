using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.OdaKullanimBloklari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.OdaKullanimBloklari.Repositories;

public class OdaKullanimBlokRepository : BaseRdbmsRepository<OdaKullanimBlok, int>, IOdaKullanimBlokRepository
{
    public OdaKullanimBlokRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}


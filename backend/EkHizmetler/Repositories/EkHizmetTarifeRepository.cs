using AutoMapper;
using STYS.EkHizmetler.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.EkHizmetler.Repositories;

public class EkHizmetTarifeRepository : BaseRdbmsRepository<EkHizmetTarife, int>, IEkHizmetTarifeRepository
{
    public EkHizmetTarifeRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

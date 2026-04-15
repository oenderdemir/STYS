using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKartlari.Repositories;

public class TasinirKartRepository : BaseRdbmsRepository<TasinirKart, int>, ITasinirKartRepository
{
    public TasinirKartRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

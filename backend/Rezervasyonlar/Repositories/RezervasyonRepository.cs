using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Rezervasyonlar.Repositories;

public class RezervasyonRepository : BaseRdbmsRepository<Rezervasyon, int>, IRezervasyonRepository
{
    public RezervasyonRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

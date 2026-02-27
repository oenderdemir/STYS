using AutoMapper;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Binalar.Repositories;

public class BinaRepository : BaseRdbmsRepository<Bina, int>, IBinaRepository
{
    public BinaRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
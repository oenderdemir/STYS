using AutoMapper;
using STYS.Iller.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Iller.Repositories;

public class IlRepository : BaseRdbmsRepository<Il, int>, IIlRepository
{
    public IlRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
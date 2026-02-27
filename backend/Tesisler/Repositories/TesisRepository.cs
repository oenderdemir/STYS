using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Tesisler.Repositories;

public class TesisRepository : BaseRdbmsRepository<Tesis, int>, ITesisRepository
{
    public TesisRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
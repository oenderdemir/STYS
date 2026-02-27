using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.IsletmeAlanlari.Repositories;

public class IsletmeAlaniRepository : BaseRdbmsRepository<IsletmeAlani, int>, IIsletmeAlaniRepository
{
    public IsletmeAlaniRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}
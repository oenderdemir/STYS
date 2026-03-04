using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.IsletmeAlanlari.Repositories;

public class IsletmeAlaniSinifiRepository : BaseRdbmsRepository<IsletmeAlaniSinifi, int>, IIsletmeAlaniSinifiRepository
{
    public IsletmeAlaniSinifiRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

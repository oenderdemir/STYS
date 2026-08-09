using AutoMapper;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Entegrasyonlar.Pos.Repositories;

public sealed class PosCihaziRepository : BaseRdbmsRepository<PosCihazi, int>, IPosCihaziRepository
{
    public PosCihaziRepository(StysAppDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
    {
    }
}

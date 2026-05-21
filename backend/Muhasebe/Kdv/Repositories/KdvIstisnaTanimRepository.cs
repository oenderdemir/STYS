using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Kdv.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.Kdv.Repositories;

public class KdvIstisnaTanimRepository
    : BaseRdbmsRepository<KdvIstisnaTanim, int>,
      IKdvIstisnaTanimRepository
{
    public KdvIstisnaTanimRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

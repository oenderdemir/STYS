using AutoMapper;
using STYS.Binalar.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Binalar.Repositories;

public class BinaYoneticiRepository : BaseRdbmsRepository<BinaYonetici, int>, IBinaYoneticiRepository
{
    public BinaYoneticiRepository(StysAppDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}

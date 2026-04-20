using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Tesisler.Repositories;

public class TesisMuhasebeciRepository : BaseRdbmsRepository<TesisMuhasebeci, int>, ITesisMuhasebeciRepository
{
    public TesisMuhasebeciRepository(StysAppDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}

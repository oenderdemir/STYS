using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Tesisler.Repositories;

public class TesisYoneticiRepository : BaseRdbmsRepository<TesisYonetici, int>, ITesisYoneticiRepository
{
    public TesisYoneticiRepository(StysAppDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}

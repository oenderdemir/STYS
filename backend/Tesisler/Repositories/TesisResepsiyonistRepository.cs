using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Tesisler.Repositories;

public class TesisResepsiyonistRepository : BaseRdbmsRepository<TesisResepsiyonist, int>, ITesisResepsiyonistRepository
{
    public TesisResepsiyonistRepository(StysAppDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}

using AutoMapper;
using STYS.Fiyatlandirma.Entities;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Fiyatlandirma.Repositories;

public class IndirimKuraliRepository : BaseRdbmsRepository<IndirimKurali, int>, IIndirimKuraliRepository
{
    public IndirimKuraliRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

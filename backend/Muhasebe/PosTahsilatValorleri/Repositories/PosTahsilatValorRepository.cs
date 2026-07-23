using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.PosTahsilatValorleri.Repositories;

public class PosTahsilatValorRepository : BaseRdbmsRepository<PosTahsilatValor, int>, IPosTahsilatValorRepository
{
    public PosTahsilatValorRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

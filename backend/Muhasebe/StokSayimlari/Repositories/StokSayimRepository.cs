using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.StokSayimlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokSayimlari.Repositories;

public class StokSayimRepository : BaseRdbmsRepository<StokSayim, int>, IStokSayimRepository
{
    public StokSayimRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

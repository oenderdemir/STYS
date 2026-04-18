using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;

public class MuhasebeHesapPlaniRepository : BaseRdbmsRepository<MuhasebeHesapPlani, int>, IMuhasebeHesapPlaniRepository
{
    public MuhasebeHesapPlaniRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}

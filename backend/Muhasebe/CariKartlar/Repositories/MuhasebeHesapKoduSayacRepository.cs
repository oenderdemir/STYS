using AutoMapper;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.CariKartlar.Repositories;

public class MuhasebeHesapKoduSayacRepository : BaseRdbmsRepository<MuhasebeHesapKoduSayac, int>, IMuhasebeHesapKoduSayacRepository
{
    public MuhasebeHesapKoduSayacRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
    }
}


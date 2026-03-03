using AutoMapper;
using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Entities;
using STYS.MisafirTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.MisafirTipleri.Services;

public class MisafirTipiService : BaseRdbmsService<MisafirTipiDto, MisafirTipi, int>, IMisafirTipiService
{
    public MisafirTipiService(IMisafirTipiRepository misafirTipiRepository, IMapper mapper)
        : base(misafirTipiRepository, mapper)
    {
    }
}

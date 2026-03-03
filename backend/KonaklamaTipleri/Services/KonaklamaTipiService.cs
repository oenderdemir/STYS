using AutoMapper;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;
using STYS.KonaklamaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.KonaklamaTipleri.Services;

public class KonaklamaTipiService : BaseRdbmsService<KonaklamaTipiDto, KonaklamaTipi, int>, IKonaklamaTipiService
{
    public KonaklamaTipiService(IKonaklamaTipiRepository konaklamaTipiRepository, IMapper mapper)
        : base(konaklamaTipiRepository, mapper)
    {
    }
}

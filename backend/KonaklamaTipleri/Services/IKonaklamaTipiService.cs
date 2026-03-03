using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.KonaklamaTipleri.Services;

public interface IKonaklamaTipiService : IBaseRdbmsService<KonaklamaTipiDto, KonaklamaTipi, int>
{
}

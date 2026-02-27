using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.OdaTipleri.Services;

public interface IOdaTipiService : IBaseRdbmsService<OdaTipiDto, OdaTipi, int>
{
}
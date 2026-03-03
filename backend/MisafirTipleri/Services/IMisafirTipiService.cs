using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.MisafirTipleri.Services;

public interface IMisafirTipiService : IBaseRdbmsService<MisafirTipiDto, MisafirTipi, int>
{
}

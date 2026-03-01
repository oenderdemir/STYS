using STYS.OdaOzellikleri.Dto;
using STYS.OdaOzellikleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.OdaOzellikleri.Services;

public interface IOdaOzellikService : IBaseRdbmsService<OdaOzellikDto, OdaOzellik, int>
{
}

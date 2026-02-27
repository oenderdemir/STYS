using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Odalar.Services;

public interface IOdaService : IBaseRdbmsService<OdaDto, Oda, int>
{
}
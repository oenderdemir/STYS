using STYS.OdaSiniflari.Dto;
using STYS.OdaSiniflari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.OdaSiniflari.Services;

public interface IOdaSinifiService : IBaseRdbmsService<OdaSinifiDto, OdaSinifi, int>
{
}

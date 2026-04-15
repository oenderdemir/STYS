using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.Depolar.Services;

public interface IDepoService : IBaseRdbmsService<DepoDto, Depo, int>
{
}

using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Kamp.Services;

public interface IKampProgramiService : IBaseRdbmsService<KampProgramiDto, KampProgrami, int>
{
}

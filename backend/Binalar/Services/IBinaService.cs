using STYS.Binalar.Dto;
using STYS.Binalar.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Binalar.Services;

public interface IBinaService : IBaseRdbmsService<BinaDto, Bina, int>
{
}
using STYS.SezonKurallari.Dto;
using STYS.SezonKurallari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.SezonKurallari.Services;

public interface ISezonKuraliService : IBaseRdbmsService<SezonKuraliDto, SezonKurali, int>
{
}

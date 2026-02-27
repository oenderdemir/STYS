using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Tesisler.Services;

public interface ITesisService : IBaseRdbmsService<TesisDto, Tesis, int>
{
}
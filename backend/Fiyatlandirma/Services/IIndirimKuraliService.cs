using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Fiyatlandirma.Services;

public interface IIndirimKuraliService : IBaseRdbmsService<IndirimKuraliDto, IndirimKurali, int>
{
}

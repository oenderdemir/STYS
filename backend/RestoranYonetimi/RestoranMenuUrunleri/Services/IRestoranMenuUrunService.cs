using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.RestoranMenuUrunleri.Services;

public interface IRestoranMenuUrunService : IBaseRdbmsService<RestoranMenuUrunDto, RestoranMenuUrun, int>
{
}

using STYS.Muhasebe.BankaHareketleri.Dtos;
using STYS.Muhasebe.BankaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.BankaHareketleri.Services;

public interface IBankaHareketService : IBaseRdbmsService<BankaHareketDto, BankaHareket, int>
{
}

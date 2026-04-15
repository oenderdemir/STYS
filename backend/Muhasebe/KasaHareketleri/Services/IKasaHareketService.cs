using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.KasaHareketleri.Services;

public interface IKasaHareketService : IBaseRdbmsService<KasaHareketDto, KasaHareket, int>
{
}

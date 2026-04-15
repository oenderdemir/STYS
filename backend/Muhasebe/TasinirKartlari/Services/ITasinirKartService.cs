using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKartlari.Services;

public interface ITasinirKartService : IBaseRdbmsService<TasinirKartDto, TasinirKart, int>
{
}

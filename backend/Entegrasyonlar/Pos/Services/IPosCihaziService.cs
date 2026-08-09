using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosCihaziService : IBaseRdbmsService<PosCihaziDto, PosCihazi, int>
{
}

using STYS.KantinYonetimi.KantinSatislari.Dtos;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public interface IKantinSatisMuhasebeFisService
{
    Task<KantinSatisDto> MuhasebeFisiOlusturAsync(int kantinSatisId, CancellationToken cancellationToken = default);
}

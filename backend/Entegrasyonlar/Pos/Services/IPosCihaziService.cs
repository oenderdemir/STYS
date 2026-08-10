using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Entities;
using STYS.Agent.Contracts.Dtos;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosCihaziService : IBaseRdbmsService<PosCihaziDto, PosCihazi, int>
{
    Task<AgentCommandDto> PairingAsync(int id, string requestedBy, CancellationToken cancellationToken);
    Task<AgentCommandDto> PingAsync(int id, string requestedBy, CancellationToken cancellationToken);
    Task<AgentCommandDto> GetDeviceInfoAsync(int id, string requestedBy, CancellationToken cancellationToken);
}

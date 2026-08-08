using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentService
{
    Task<AgentDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgentListDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<AgentDto> CreateAsync(AgentKaydetRequest request, string createdBy, CancellationToken cancellationToken);
    Task<AgentDto> UpdateAsync(int id, AgentKaydetRequest request, CancellationToken cancellationToken);
    Task ApproveAsync(int id, CancellationToken cancellationToken);
    Task DisableAsync(int id, CancellationToken cancellationToken);
    Task RevokeAsync(int id, CancellationToken cancellationToken);
    Task<AgentEnrollmentCodeDto> GenerateEnrollmentCodeAsync(AgentEnrollmentCodeRequest request, string createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgentEnrollmentCodeDto>> GetEnrollmentCodesAsync(CancellationToken cancellationToken);
    Task RevokeEnrollmentCodeAsync(int enrollmentId, CancellationToken cancellationToken);
}

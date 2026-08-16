using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentService
{
    Task<AgentDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgentListDto>> GetAllAsync(int? kurumId, int? tesisId, CancellationToken cancellationToken);
    Task<AgentDto> CreateAsync(AgentKaydetRequest request, string createdBy, CancellationToken cancellationToken);
    Task<AgentDto> UpdateAsync(int id, AgentKaydetRequest request, CancellationToken cancellationToken);
    Task UpdateScopesAsync(int id, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken);
    Task ApproveAsync(int id, string approvedBy, CancellationToken cancellationToken);
    Task RejectAsync(int id, string rejectedBy, CancellationToken cancellationToken);
    Task DisableAsync(int id, CancellationToken cancellationToken);
    Task RevokeAsync(int id, CancellationToken cancellationToken);
    Task<AgentEnrollmentCodeDto> GenerateEnrollmentCodeAsync(AgentEnrollmentCodeRequest request, string createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgentEnrollmentCodeDto>> GetEnrollmentCodesAsync(int? kurumId, int? tesisId, CancellationToken cancellationToken);
    Task RevokeEnrollmentCodeAsync(int enrollmentId, CancellationToken cancellationToken);
}

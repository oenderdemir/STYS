using System.Threading;
using System.Threading.Tasks;

namespace STYS.Muhasebe.DevTools.Services;

public interface IMuhasebeSmokeTestSeedService
{
    Task<MuhasebeSmokeTestSeedResultDto> SeedAsync(CancellationToken cancellationToken = default);
}

public sealed record MuhasebeSmokeTestSeedResultDto
{
    public required string EnvironmentName { get; init; }
    public required string TestUserName { get; init; }
    public required string TestTesisName { get; init; }
    public required string ForbiddenTesisName { get; init; }
    public required DateTimeOffset SeededAt { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

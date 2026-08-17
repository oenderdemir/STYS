using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Contracts.Versioning;

namespace STYS.Agent.Updater.Services;

public interface IAgentHealthProbe
{
    Task<bool> WaitForHealthyAsync(int localUiPort, string targetVersion, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class AgentHealthProbe : IAgentHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentHealthProbe> _logger;

    public AgentHealthProbe(HttpClient httpClient, ILogger<AgentHealthProbe> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> WaitForHealthyAsync(int localUiPort, string targetVersion, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await IsHealthyAsync(localUiPort, targetVersion, cancellationToken))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return false;
    }

    private async Task<bool> IsHealthyAsync(int localUiPort, string targetVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"http://127.0.0.1:{localUiPort}/api/bootstrap/diagnostics", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryGetPropertyIgnoreCase(doc.RootElement, "startupHealthy", out var healthyElement) || !healthyElement.GetBoolean())
            {
                return false;
            }

            // Fail closed: an agent that does not report a version is not evidence that the new
            // build is running. Treating a missing or blank value as "good enough" would let a
            // failed upgrade pass the health gate and skip the rollback.
            if (!TryGetPropertyIgnoreCase(doc.RootElement, "agentVersion", out var versionElement))
            {
                _logger.LogWarning("Agent health response reported no version. Expected={Expected}", targetVersion);
                return false;
            }

            var reportedVersion = versionElement.ValueKind == JsonValueKind.String ? versionElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(reportedVersion))
            {
                _logger.LogWarning("Agent health response reported an empty version. Expected={Expected}", targetVersion);
                return false;
            }

            // Compare release identity, not the raw string: the SDK appends "+<commit-sha>" to the
            // informational version in a git build, which would otherwise make a healthy upgrade
            // look like the wrong binary and trigger a rollback.
            if (!AgentVersionComparison.SameRelease(reportedVersion, targetVersion))
            {
                _logger.LogWarning("Agent version mismatch after upgrade. Expected={Expected}, Actual={Actual}", targetVersion, reportedVersion);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Agent health probe failed.");
            return false;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

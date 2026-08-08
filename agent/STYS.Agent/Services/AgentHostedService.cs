using STYS.Agent.Client;
using STYS.Agent.Contracts.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace STYS.Agent.Services;

public sealed class AgentHostedService : IHostedService
{
    private readonly IStysAgentApiClient _client;
    private readonly StysAgentClientOptions _options;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        IStysAgentApiClient client,
        IOptions<StysAgentClientOptions> options,
        ILogger<AgentHostedService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("STYS Agent başlatılıyor...");

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            _logger.LogWarning("Agent kimlik bilgileri yapılandırılmamış. Enrollment gerekli.");
            return;
        }

        try
        {
            await _client.GetTokenAsync(new AgentTokenRequest
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
                AgentInstanceId = _options.AgentInstanceId,
                AgentVersion = _options.AgentVersion
            }, cancellationToken);

            _logger.LogInformation("Agent başarıyla kimlik doğruladı.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent kimlik doğrulama başarısız.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("STYS Agent durduruluyor.");
        return Task.CompletedTask;
    }
}

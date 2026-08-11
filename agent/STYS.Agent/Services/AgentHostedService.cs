using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Authentication;

namespace STYS.Agent.Services;

public sealed class AgentHostedService : BackgroundService
{
    private readonly IAgentEnrollmentCoordinator _enrollmentCoordinator;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        IAgentEnrollmentCoordinator enrollmentCoordinator,
        IAgentAuthenticationState authenticationState,
        ILogger<AgentHostedService> logger)
    {
        _enrollmentCoordinator = enrollmentCoordinator;
        _authenticationState = authenticationState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("STYS Agent başlatılıyor...");

        while (!stoppingToken.IsCancellationRequested && !_authenticationState.IsReady)
        {
            try
            {
                if (await _enrollmentCoordinator.TryActivateAsync(stoppingToken))
                {
                    _logger.LogInformation("Agent başarıyla kimlik doğruladı.");
                    break;
                }

                _logger.LogInformation("Agent kimlik doğrulama için beklemede.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent kimlik doğrulama başarısız. Yeniden denenecek.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

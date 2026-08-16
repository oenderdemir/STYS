using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Authentication;

namespace STYS.Agent.Services;

public sealed class AgentHostedService : BackgroundService
{
    private readonly IAgentEnrollmentCoordinator _enrollmentCoordinator;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        IAgentEnrollmentCoordinator enrollmentCoordinator,
        IAgentAuthenticationState authenticationState,
        IAgentRuntimeStatus runtimeStatus,
        ILogger<AgentHostedService> logger)
    {
        _enrollmentCoordinator = enrollmentCoordinator;
        _authenticationState = authenticationState;
        _runtimeStatus = runtimeStatus;
        _logger = logger;
    }

    /// <summary>Retry cadence while the agent is still trying to become authenticated.</summary>
    private static readonly TimeSpan ActivationRetryInterval = TimeSpan.FromSeconds(5);

    /// <summary>Slower cadence once we know the agent is registered and merely waiting for an
    /// operator decision; approval is a human action, so polling every few seconds is wasteful.</summary>
    private static readonly TimeSpan ApprovalPollInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("STYS Agent başlatılıyor...");
        var pendingApprovalLogged = false;

        while (!stoppingToken.IsCancellationRequested && !_authenticationState.IsReady)
        {
            var delay = ActivationRetryInterval;

            try
            {
                if (await _enrollmentCoordinator.TryActivateAsync(stoppingToken))
                {
                    _logger.LogInformation("Agent başarıyla kimlik doğruladı.");
                    break;
                }

                if (_runtimeStatus.PendingApproval)
                {
                    delay = ApprovalPollInterval;
                    // Log the pending state once rather than on every poll, so an agent left
                    // awaiting approval overnight does not fill the log.
                    if (!pendingApprovalLogged)
                    {
                        _logger.LogInformation("Agent STYS'e kaydedildi, onay bekleniyor.");
                        pendingApprovalLogged = true;
                    }
                }
                else
                {
                    pendingApprovalLogged = false;
                    _logger.LogInformation("Agent kimlik doğrulama için beklemede.");
                }
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
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

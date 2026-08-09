using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public sealed class AgentHostedService : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentCredentialStore _credentialStore;
    private readonly StysAgentClientOptions _options;
    private readonly AgentTokenStore _tokenStore;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        IStysAgentApiClient client,
        IAgentCredentialStore credentialStore,
        IOptions<StysAgentClientOptions> options,
        AgentTokenStore tokenStore,
        IAgentAuthenticationState authenticationState,
        ILogger<AgentHostedService> logger)
    {
        _client = client;
        _credentialStore = credentialStore;
        _options = options.Value;
        _tokenStore = tokenStore;
        _authenticationState = authenticationState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("STYS Agent başlatılıyor...");
        await ExecuteAuthenticationLoopAsync(stoppingToken);
    }

    private async Task ExecuteAuthenticationLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_authenticationState.IsReady)
        {
            var credential = await _credentialStore.GetAsync(cancellationToken);
            if (!IsUsableCredential(credential))
            {
                if (credential is not null)
                {
                    _logger.LogWarning("Kayıtlı credential geçersiz görünüyor, temizleniyor.");
                    await _credentialStore.DeleteAsync(cancellationToken);
                }

                credential = null;
            }

            if (credential is null)
            {
                if (!await TryEnrollAsync(cancellationToken))
                {
                    await WaitBeforeRetryAsync(cancellationToken);
                    continue;
                }

                credential = await _credentialStore.GetAsync(cancellationToken);
                if (credential is null)
                {
                    await WaitBeforeRetryAsync(cancellationToken);
                    continue;
                }
            }

            _options.ClientId = credential.ClientId;
            _options.ClientSecret = credential.ClientSecret;
            _options.AgentInstanceId = credential.AgentInstanceId;

            try
            {
                var tokenResponse = await _client.GetTokenAsync(new AgentTokenRequest
                {
                    ClientId = credential.ClientId,
                    ClientSecret = credential.ClientSecret,
                    AgentInstanceId = credential.AgentInstanceId,
                    AgentVersion = _options.AgentVersion
                }, cancellationToken);

                _tokenStore.SetToken(tokenResponse);
                _authenticationState.MarkAuthenticated();
                _logger.LogInformation("Agent başarıyla kimlik doğruladı (AgentId: {AgentId}).", credential.AgentId);
                return;
            }
            catch (Exception ex)
            {
                if (await TryRecoverByReenrollingAsync(ex, cancellationToken))
                    continue;

                _logger.LogWarning(ex, "Agent kimlik doğrulama başarısız. Yeniden denenecek.");
                await WaitBeforeRetryAsync(cancellationToken);
            }
        }
    }

    private static bool IsUsableCredential(AgentLocalCredential? credential) =>
        credential is not null &&
        !string.IsNullOrWhiteSpace(credential.ClientId) &&
        !string.IsNullOrWhiteSpace(credential.ClientSecret) &&
        !string.IsNullOrWhiteSpace(credential.AgentInstanceId);

    private async Task<bool> TryEnrollAsync(CancellationToken cancellationToken)
    {
        var enrollmentCode = _options.EnrollmentCode
            ?? Environment.GetEnvironmentVariable("STYS_ENROLLMENT_CODE");

        if (string.IsNullOrWhiteSpace(enrollmentCode))
        {
            _logger.LogInformation("Enrollment kodu bulunamadı — kimlik doğrulama bekleniyor.");
            return false;
        }

        _logger.LogInformation("Enrollment kodu bulundu, kayıt başlatılıyor...");

        var agentInstanceId = GetOrCreateInstanceId();

        try
        {
            var enrollRequest = new AgentEnrollmentRequest
            {
                EnrollmentCode = enrollmentCode,
                AgentKey = Environment.MachineName,
                CihazKimligi = agentInstanceId,
                AgentVersion = _options.AgentVersion
            };

            var response = await _client.EnrollAsync(enrollRequest, cancellationToken);
            _logger.LogInformation("Enrollment başarılı. AgentId: {AgentId}", response.AgentId);

            var credential = new AgentLocalCredential
            {
                ClientId = response.ClientId,
                ClientSecret = response.ClientSecret,
                AgentInstanceId = agentInstanceId,
                AgentKey = response.AgentKey,
                AgentId = response.AgentId,
                CreatedAt = DateTime.UtcNow
            };

            await _credentialStore.SaveAsync(credential, cancellationToken);
            _logger.LogInformation("Credential güvenli şekilde kaydedildi.");

            try { Environment.SetEnvironmentVariable("STYS_ENROLLMENT_CODE", null, EnvironmentVariableTarget.Process); } catch { }

            _options.ClientId = credential.ClientId;
            _options.ClientSecret = credential.ClientSecret;
            _options.AgentInstanceId = credential.AgentInstanceId;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Enrollment başarısız. Yeniden denenecek.");
            return false;
        }
    }

    private static Task WaitBeforeRetryAsync(CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

    private async Task<bool> TryRecoverByReenrollingAsync(Exception ex, CancellationToken cancellationToken)
    {
        if (!IsAuthenticationFailure(ex))
            return false;

        var enrollmentCode = _options.EnrollmentCode
            ?? Environment.GetEnvironmentVariable("STYS_ENROLLMENT_CODE");

        if (string.IsNullOrWhiteSpace(enrollmentCode))
            return false;

        _logger.LogWarning("Kayıtlı credential geçersiz görünüyor ve enrollment code mevcut. Credential temizlenip yeniden enrollment deneniyor.");
        await _credentialStore.DeleteAsync(cancellationToken);
        return await TryEnrollAsync(cancellationToken);
    }

    private static bool IsAuthenticationFailure(Exception ex) =>
        ex is HttpRequestException httpEx &&
        httpEx.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static string GetOrCreateInstanceId()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "STYS", "Agent");
        Directory.CreateDirectory(directory);
        TrySecureDirectory(directory);
        var instanceFile = Path.Combine(directory, "instance.id");

        if (File.Exists(instanceFile))
        {
            var existing = File.ReadAllText(instanceFile).Trim();
            if (!string.IsNullOrWhiteSpace(existing) && Guid.TryParseExact(existing, "N", out _))
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(instanceFile, id);
        TrySecureFile(instanceFile);
        return id;
    }

    private static void TrySecureFile(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
    }

    private static void TrySecureDirectory(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { }
    }
}

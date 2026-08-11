using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Configuration;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Services;

public sealed class AgentEnrollmentCoordinator : IAgentEnrollmentCoordinator
{
    private readonly IAgentBootstrapConfigurationStore _bootstrapStore;
    private readonly IAgentBootstrapConnectionTester _connectionTester;
    private readonly IAgentCredentialStore _credentialStore;
    private readonly IStysAgentApiClient _client;
    private readonly AgentTokenStore _tokenStore;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly IAgentPathResolver _paths;
    private readonly StysAgentClientOptions _clientOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<AgentEnrollmentCoordinator> _logger;

    public AgentEnrollmentCoordinator(
        IAgentBootstrapConfigurationStore bootstrapStore,
        IAgentBootstrapConnectionTester connectionTester,
        IAgentCredentialStore credentialStore,
        IStysAgentApiClient client,
        AgentTokenStore tokenStore,
        IAgentAuthenticationState authenticationState,
        IAgentRuntimeStatus runtimeStatus,
        IAgentPathResolver paths,
        IOptions<StysAgentClientOptions> clientOptions,
        ILogger<AgentEnrollmentCoordinator> logger)
    {
        _bootstrapStore = bootstrapStore;
        _connectionTester = connectionTester;
        _credentialStore = credentialStore;
        _client = client;
        _tokenStore = tokenStore;
        _authenticationState = authenticationState;
        _runtimeStatus = runtimeStatus;
        _paths = paths;
        _clientOptions = clientOptions.Value;
        _logger = logger;
    }

    public async Task<AgentBootstrapEnrollmentResult> EnrollAsync(AgentBootstrapEnrollmentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_authenticationState.IsReady || await HasUsableCredentialAsync(cancellationToken))
            {
                _runtimeStatus.MarkCredentialPresent(true);
                return new AgentBootstrapEnrollmentResult
                {
                    Success = true,
                    Message = "Agent zaten kayıtlı.",
                    AgentId = (await _credentialStore.GetAsync(cancellationToken))?.AgentId ?? 0,
                    AgentDisplayName = request.AgentDisplayName.Trim(),
                    CredentialSaved = true,
                    TokenAcquired = true,
                    RestartRequired = false
                };
            }

            return await EnrollCoreAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local enrollment failed.");
            return new AgentBootstrapEnrollmentResult
            {
                Success = false,
                Message = MapErrorMessage(ex),
                RestartRequired = false
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> TryActivateAsync(CancellationToken cancellationToken)
    {
        if (_authenticationState.IsReady)
            return true;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_authenticationState.IsReady)
                return true;

            var credential = await _credentialStore.GetAsync(cancellationToken);
            var config = await _bootstrapStore.GetAsync(cancellationToken);
            if (credential is null)
            {
                var enrollmentCode = ResolveEnrollmentCode();
                if (string.IsNullOrWhiteSpace(enrollmentCode))
                    return false;

                var enrollmentResult = await EnrollCoreAsync(new AgentBootstrapEnrollmentRequest
                {
                    StysBaseUrl = config.StysBaseUrl,
                    AgentDisplayName = config.AgentDisplayName,
                    EnrollmentCode = enrollmentCode,
                    HttpTimeoutSeconds = config.HttpTimeoutSeconds,
                    LocalUiPort = config.LocalUiPort,
                    Capabilities = []
                }, cancellationToken);

                return enrollmentResult.Success;
            }

            if (!string.Equals(NormalizeBaseUrl(credential.EnrollmentBaseUrl), NormalizeBaseUrl(config.StysBaseUrl), StringComparison.OrdinalIgnoreCase))
            {
                _runtimeStatus.MarkReEnrollmentRequired("Mevcut local credential bu STYS adresi için geçerli değil.");
                _authenticationState.Reset();
                _tokenStore.ClearToken();
                _clientOptions.ClientId = string.Empty;
                _clientOptions.ClientSecret = string.Empty;
                _clientOptions.AgentInstanceId = string.Empty;
                return false;
            }

            _clientOptions.ClientId = credential.ClientId;
            _clientOptions.ClientSecret = credential.ClientSecret;
            _clientOptions.AgentInstanceId = credential.AgentInstanceId;
            _runtimeStatus.MarkCredentialPresent(true);

            var token = await _client.GetTokenAsync(new AgentTokenRequest
            {
                ClientId = credential.ClientId,
                ClientSecret = credential.ClientSecret,
                AgentInstanceId = credential.AgentInstanceId,
                AgentVersion = _clientOptions.AgentVersion
            }, cancellationToken);

            _tokenStore.SetToken(token);
            _authenticationState.MarkAuthenticated();
            _runtimeStatus.MarkAuthenticated();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Authentication activation attempt failed.");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AgentBootstrapEnrollmentResult> EnrollCoreAsync(AgentBootstrapEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var test = await _connectionTester.TestAsync(request.StysBaseUrl, request.HttpTimeoutSeconds, cancellationToken);
        if (!test.Success)
        {
            _runtimeStatus.MarkFailedConnection(test.Message);
            throw new BaseException(test.Message ?? "STYS erişilemiyor.", 400);
        }

        _runtimeStatus.MarkSuccessfulConnection();

        var storedConfig = await _bootstrapStore.GetAsync(cancellationToken);
        storedConfig.StysBaseUrl = request.StysBaseUrl.TrimEnd('/');
        storedConfig.AgentDisplayName = NormalizeDisplayName(request.AgentDisplayName);
        storedConfig.HttpTimeoutSeconds = request.HttpTimeoutSeconds;
        storedConfig.LocalUiPort = request.LocalUiPort;
        await _bootstrapStore.SaveAsync(storedConfig, cancellationToken);

        _clientOptions.BaseUrl = storedConfig.StysBaseUrl;
        _clientOptions.RequestTimeoutSeconds = storedConfig.HttpTimeoutSeconds;

        var agentInstanceId = GetOrCreateInstanceId();
        var agentDisplayName = NormalizeDisplayName(request.AgentDisplayName);
        var enrollmentRequest = new AgentEnrollmentRequest
        {
            EnrollmentCode = request.EnrollmentCode.Trim(),
            AgentKey = Environment.MachineName,
            AgentDisplayName = agentDisplayName,
            CihazKimligi = agentInstanceId,
            AgentVersion = _clientOptions.AgentVersion,
            Capabilities = request.Capabilities ?? []
        };

        var enrollmentResponse = await _client.EnrollAsync(enrollmentRequest, cancellationToken);
        var credential = new AgentLocalCredential
        {
            ClientId = enrollmentResponse.ClientId,
            ClientSecret = enrollmentResponse.ClientSecret,
            AgentInstanceId = agentInstanceId,
            AgentKey = enrollmentResponse.AgentKey,
            EnrollmentBaseUrl = storedConfig.StysBaseUrl,
            AgentId = enrollmentResponse.AgentId,
            CreatedAt = DateTime.UtcNow
        };

        await _credentialStore.SaveAsync(credential, cancellationToken);
        _clientOptions.ClientId = credential.ClientId;
        _clientOptions.ClientSecret = credential.ClientSecret;
        _clientOptions.AgentInstanceId = credential.AgentInstanceId;

        try
        {
            Environment.SetEnvironmentVariable("STYS_ENROLLMENT_CODE", null, EnvironmentVariableTarget.Process);
        }
        catch
        {
        }

        var token = await _client.GetTokenAsync(new AgentTokenRequest
        {
            ClientId = credential.ClientId,
            ClientSecret = credential.ClientSecret,
            AgentInstanceId = credential.AgentInstanceId,
            AgentVersion = _clientOptions.AgentVersion
        }, cancellationToken);

        _tokenStore.SetToken(token);
        _authenticationState.MarkAuthenticated();
        _runtimeStatus.MarkCredentialPresent(true);
        _runtimeStatus.MarkAuthenticated();

        return new AgentBootstrapEnrollmentResult
        {
            Success = true,
            Message = enrollmentResponse.Message ?? "✓ STYS'e kayıt başarılı",
            AgentId = enrollmentResponse.AgentId,
            AgentDisplayName = agentDisplayName,
            CredentialSaved = true,
            TokenAcquired = true,
            RestartRequired = false
        };
    }

    private async Task<bool> HasUsableCredentialAsync(CancellationToken cancellationToken)
    {
        var config = await _bootstrapStore.GetAsync(cancellationToken);
        var credential = await _credentialStore.GetAsync(cancellationToken);
        var usable = credential is not null
            && !string.IsNullOrWhiteSpace(credential.ClientId)
            && !string.IsNullOrWhiteSpace(credential.ClientSecret)
            && !string.IsNullOrWhiteSpace(credential.AgentInstanceId)
            && string.Equals(NormalizeBaseUrl(credential.EnrollmentBaseUrl), NormalizeBaseUrl(config.StysBaseUrl), StringComparison.OrdinalIgnoreCase);

        if (credential is not null && !usable)
        {
            _runtimeStatus.MarkReEnrollmentRequired("Mevcut local credential bu STYS adresi için geçerli değil.");
            _authenticationState.Reset();
            _tokenStore.ClearToken();
            _clientOptions.ClientId = string.Empty;
            _clientOptions.ClientSecret = string.Empty;
            _clientOptions.AgentInstanceId = string.Empty;
        }

        return usable;
    }

    private static void ValidateRequest(AgentBootstrapEnrollmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StysBaseUrl))
            throw new ArgumentException("STYS Sunucu Adresi zorunludur.");

        if (!Uri.TryCreate(request.StysBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Geçersiz STYS adresi.");
        }

        if (string.IsNullOrWhiteSpace(request.EnrollmentCode))
            throw new ArgumentException("Enrollment kodu zorunludur.");

        if (string.IsNullOrWhiteSpace(request.AgentDisplayName))
            throw new ArgumentException("Agent adı zorunludur.");

        if (request.HttpTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.HttpTimeoutSeconds));

        if (request.LocalUiPort <= 0 || request.LocalUiPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(request.LocalUiPort));
    }

    private string ResolveEnrollmentCode()
    {
        if (!string.IsNullOrWhiteSpace(_clientOptions.EnrollmentCode))
            return _clientOptions.EnrollmentCode!;

        return Environment.GetEnvironmentVariable("STYS_ENROLLMENT_CODE") ?? string.Empty;
    }

    private string GetOrCreateInstanceId()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var instanceFile = _paths.InstanceIdPath;

        if (File.Exists(instanceFile))
        {
            var existing = File.ReadAllText(instanceFile).Trim();
            if (!string.IsNullOrWhiteSpace(existing) && Guid.TryParseExact(existing, "N", out _))
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(instanceFile, id);
        return id;
    }

    private static string NormalizeDisplayName(string value) =>
        string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value.Trim();

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:7160" : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static string MapErrorMessage(Exception ex)
    {
        if (ex is AgentApiException apiEx)
        {
            var message = apiEx.Message;
            if (apiEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return "Enrollment reddedildi.";
            if (apiEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return "Enrollment reddedildi.";
            if (apiEx.StatusCode == System.Net.HttpStatusCode.RequestTimeout
                || apiEx.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                || apiEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                return "STYS erişilemiyor.";

            if (message.Contains("geçersiz enrollment", StringComparison.OrdinalIgnoreCase))
                return "Enrollment code geçersiz.";
            if (message.Contains("kullanım sayısına ulaştı", StringComparison.OrdinalIgnoreCase))
                return "Enrollment code kullanılmış.";
            if (message.Contains("süresi dolmuş", StringComparison.OrdinalIgnoreCase))
                return "Enrollment code süresi dolmuş.";
            if (message.Contains("tls", StringComparison.OrdinalIgnoreCase) || message.Contains("certificate", StringComparison.OrdinalIgnoreCase))
                return "TLS hatası.";
            if (message.Contains("credential", StringComparison.OrdinalIgnoreCase) || message.Contains("secret", StringComparison.OrdinalIgnoreCase))
                return "Credential kaydedilemedi.";
            if (message.Contains("token", StringComparison.OrdinalIgnoreCase))
                return "Token alınamadı.";

            return message;
        }

        if (ex is BaseException baseException)
        {
            return baseException.Message ?? "İşlem başarısız.";
        }

        if (ex is HttpRequestException httpEx && httpEx.StatusCode is not null)
        {
            return httpEx.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Enrollment reddedildi.",
                System.Net.HttpStatusCode.Forbidden => "Enrollment reddedildi.",
                System.Net.HttpStatusCode.RequestTimeout => "STYS erişilemiyor.",
                System.Net.HttpStatusCode.GatewayTimeout => "STYS erişilemiyor.",
                System.Net.HttpStatusCode.ServiceUnavailable => "STYS erişilemiyor.",
                _ => httpEx.Message
            };
        }

        return ex.Message;
    }
}

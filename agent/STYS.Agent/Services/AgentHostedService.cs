using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public sealed class AgentHostedService : IHostedService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentCredentialStore _credentialStore;
    private readonly StysAgentClientOptions _options;
    private readonly AgentTokenStore _tokenStore;
    private readonly ILogger<AgentHostedService> _logger;

    public AgentHostedService(
        IStysAgentApiClient client,
        IAgentCredentialStore credentialStore,
        IOptions<StysAgentClientOptions> options,
        AgentTokenStore tokenStore,
        ILogger<AgentHostedService> logger)
    {
        _client = client;
        _credentialStore = credentialStore;
        _options = options.Value;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("STYS Agent başlatılıyor...");

        var credential = await _credentialStore.GetAsync(cancellationToken);

        if (credential is null)
        {
            await TryEnrollAsync(cancellationToken);
            credential = await _credentialStore.GetAsync(cancellationToken);
        }

        if (credential is null)
        {
            _logger.LogWarning("Agent enrollment başarısız — konfigürasyon kontrol edilmeli.");
            return;
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
            _logger.LogInformation("Agent başarıyla kimlik doğruladı (AgentId: {AgentId}).", credential.AgentId);
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

    private async Task TryEnrollAsync(CancellationToken cancellationToken)
    {
        var enrollmentCode = _options.EnrollmentCode
            ?? Environment.GetEnvironmentVariable("STYS_ENROLLMENT_CODE");

        if (string.IsNullOrWhiteSpace(enrollmentCode))
        {
            _logger.LogInformation("Enrollment kodu bulunamadı — kayıt yapılmayacak.");
            return;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enrollment başarısız.");
        }
    }

    private static string GetOrCreateInstanceId()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "STYS", "Agent");
        Directory.CreateDirectory(directory);
        var instanceFile = Path.Combine(directory, "instance.id");

        if (File.Exists(instanceFile))
        {
            var existing = File.ReadAllText(instanceFile).Trim();
            if (!string.IsNullOrWhiteSpace(existing) && Guid.TryParseExact(existing, "N", out _))
                return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(instanceFile, id);
        try { File.SetUnixFileMode(instanceFile, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
        return id;
    }
}

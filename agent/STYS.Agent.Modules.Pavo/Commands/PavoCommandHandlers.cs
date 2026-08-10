using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo.Commands;

public sealed class PavoPairingCommandHandler : IAgentCommandHandler<PavoPairingCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPavoRestClient _client;
    private readonly ILogger<PavoPairingCommandHandler> _logger;

    public PavoPairingCommandHandler(IPavoRestClient client, ILogger<PavoPairingCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoPairingCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PAVO pairing başlatılıyor: {PosCihaziId}", command.PosCihaziId);
            var response = await _client.PairingAsync(ToRequest(command), cancellationToken);
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            if (response.HasError || response.HasAbondon || !response.OnayliMi)
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode ?? "PAVO_PAIRING_REJECTED",
                    ErrorMessage = response.Message ?? "PAVO pairing reddedildi."
                };
            }

            return AgentCommandResult.Ok(payload);
        }
        catch (PavoRestClientException ex)
        {
            _logger.LogWarning(ex, "PAVO pairing transport error");
            return new AgentCommandResult
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    private static PavoPairingRequest ToRequest(PavoPairingCommand command) => new()
    {
        PosCihaziId = command.PosCihaziId,
        KurumId = command.KurumId,
        TesisId = command.TesisId,
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        CurrentFingerprint = command.Fingerprint,
        TransactionHandle = command.TransactionHandle
    };
}

public sealed class PavoPingCommandHandler : IAgentCommandHandler<PavoPingCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPavoRestClient _client;
    private readonly ILogger<PavoPingCommandHandler> _logger;

    public PavoPingCommandHandler(IPavoRestClient client, ILogger<PavoPingCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoPingCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PAVO ping gönderiliyor: {PosCihaziId}", command.PosCihaziId);
            var response = await _client.PingAsync(ToRequest(command), cancellationToken);
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            if (response.HasError || response.HasAbondon)
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode ?? "PAVO_PING_FAILED",
                    ErrorMessage = response.Message ?? "PAVO ping başarısız."
                };
            }

            return AgentCommandResult.Ok(payload);
        }
        catch (PavoRestClientException ex)
        {
            _logger.LogWarning(ex, "PAVO ping transport error");
            return new AgentCommandResult
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    private static PavoPingRequest ToRequest(PavoPingCommand command) => new()
    {
        PosCihaziId = command.PosCihaziId,
        KurumId = command.KurumId,
        TesisId = command.TesisId,
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        TransactionHandle = command.TransactionHandle
    };
}

public sealed class PavoGetDeviceInfoCommandHandler : IAgentCommandHandler<PavoGetDeviceInfoCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPavoRestClient _client;
    private readonly ILogger<PavoGetDeviceInfoCommandHandler> _logger;

    public PavoGetDeviceInfoCommandHandler(IPavoRestClient client, ILogger<PavoGetDeviceInfoCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoGetDeviceInfoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PAVO device info alınuyor: {PosCihaziId}", command.PosCihaziId);
            var response = await _client.GetDeviceInfoAsync(ToRequest(command), cancellationToken);
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            if (response.HasError || response.HasAbondon)
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode ?? "PAVO_DEVICE_INFO_FAILED",
                    ErrorMessage = response.Message ?? "PAVO device info alınamadı."
                };
            }

            return AgentCommandResult.Ok(payload);
        }
        catch (PavoRestClientException ex)
        {
            _logger.LogWarning(ex, "PAVO device info transport error");
            return new AgentCommandResult
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    private static PavoGetDeviceInfoRequest ToRequest(PavoGetDeviceInfoCommand command) => new()
    {
        PosCihaziId = command.PosCihaziId,
        KurumId = command.KurumId,
        TesisId = command.TesisId,
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        TransactionHandle = command.TransactionHandle
    };
}

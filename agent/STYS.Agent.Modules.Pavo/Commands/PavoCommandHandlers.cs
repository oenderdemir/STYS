using System.Globalization;
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
            if (!PavoResponseHelpers.IsSuccessful(response))
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "PAVO_PAIRING_REJECTED",
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
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        // The stable client fingerprint always travels on TransactionHandle.Fingerprint (assigned
        // during command sequence preparation); CurrentFingerprint just mirrors it for diagnostics.
        CurrentFingerprint = command.TransactionHandle.Fingerprint,
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
            if (!PavoResponseHelpers.IsSuccessful(response))
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "PAVO_PING_FAILED",
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
            if (!PavoResponseHelpers.IsSuccessful(response))
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? "PAVO_DEVICE_INFO_FAILED",
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
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        TransactionHandle = command.TransactionHandle
    };
}

public sealed class PavoStartPaymentCommandHandler : IAgentCommandHandler<PavoStartPaymentCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPavoRestClient _client;
    private readonly ILogger<PavoStartPaymentCommandHandler> _logger;

    public PavoStartPaymentCommandHandler(IPavoRestClient client, ILogger<PavoStartPaymentCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoStartPaymentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PAVO ödeme başlatılıyor: {PosOdemeIslemiId} / {SaleReference}", command.PosOdemeIslemiId, command.SaleReference);
            var response = await _client.StartPaymentAsync(ToRequest(command), cancellationToken);
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            // Reference StartPayment success is stricter than the common envelope check: the payment
            // itself must report Data.IsSuccessful. A clean envelope with a declined payment is a
            // failed command.
            if (!PavoResponseHelpers.IsPaymentSuccessful(response))
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? response.Data?.ResponseCode ?? response.Data?.ResultCode ?? "PAVO_START_PAYMENT_FAILED",
                    ErrorMessage = response.Message ?? response.Data?.FailMessage ?? response.Data?.CevapAciklama ?? response.Data?.Message ?? "PAVO ödeme başlatılamadı."
                };
            }

            return AgentCommandResult.Ok(payload);
        }
        catch (PavoRestClientException ex)
        {
            _logger.LogWarning(ex, "PAVO start payment transport error");
            return new AgentCommandResult
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    private static PavoStartPaymentRequest ToRequest(PavoStartPaymentCommand command) => new()
    {
        PosCihaziId = command.PosCihaziId,
        PosOdemeIslemiId = command.PosOdemeIslemiId,
        PosTerminalId = command.PosTerminalId,
        SaleReference = command.SaleReference,
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        Amount = command.Amount,
        CurrencyCode = command.CurrencyCode,
        Description = command.Description,
        TransactionHandle = command.TransactionHandle
    };
}

public sealed class PavoGetPaymentResultCommandHandler : IAgentCommandHandler<PavoGetPaymentResultCommand>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPavoRestClient _client;
    private readonly ILogger<PavoGetPaymentResultCommandHandler> _logger;

    public PavoGetPaymentResultCommandHandler(IPavoRestClient client, ILogger<PavoGetPaymentResultCommandHandler> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<AgentCommandResult> HandleAsync(PavoGetPaymentResultCommand command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("PAVO ödeme sonucu sorgulanıyor: {PosOdemeIslemiId} / {SaleReference}", command.PosOdemeIslemiId, command.SaleReference);
            var response = await _client.GetPaymentResultAsync(ToRequest(command), cancellationToken);
            var payload = JsonSerializer.Serialize(response, JsonOptions);
            if (!PavoResponseHelpers.IsSuccessful(response))
            {
                return new AgentCommandResult
                {
                    Success = false,
                    ResultPayload = payload,
                    ErrorCode = response.ErrorCode?.ToString(CultureInfo.InvariantCulture) ?? response.Data?.ResultCode ?? "PAVO_GET_PAYMENT_RESULT_FAILED",
                    ErrorMessage = response.Message ?? response.Data?.FailMessage ?? response.Data?.Message ?? "PAVO ödeme sonucu alınamadı."
                };
            }

            return AgentCommandResult.Ok(payload);
        }
        catch (PavoRestClientException ex)
        {
            _logger.LogWarning(ex, "PAVO get payment result transport error");
            return new AgentCommandResult
            {
                Success = false,
                ErrorCode = ex.ErrorCode,
                ErrorMessage = ex.Message
            };
        }
    }

    private static PavoGetPaymentResultRequest ToRequest(PavoGetPaymentResultCommand command) => new()
    {
        PosCihaziId = command.PosCihaziId,
        PosOdemeIslemiId = command.PosOdemeIslemiId,
        PosTerminalId = command.PosTerminalId,
        SaleReference = command.SaleReference,
        IpAddress = command.IpAddress,
        HttpPort = command.HttpPort,
        HttpsPort = command.HttpsPort,
        UseHttps = command.UseHttps,
        TransactionHandle = command.TransactionHandle
    };
}

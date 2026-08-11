using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;

namespace STYS.Agent.Workers;

public sealed class CommandPollingWorker : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly IAgentCommandHandlerRegistry _handlerRegistry;
    private readonly IAgentCommandExecutionStore _executionStore;
    private readonly ILogger<CommandPollingWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommandPollingWorker(
        IStysAgentApiClient client,
        IAgentAuthenticationState authenticationState,
        IAgentRuntimeStatus runtimeStatus,
        IAgentCommandHandlerRegistry handlerRegistry,
        IAgentCommandExecutionStore executionStore,
        ILogger<CommandPollingWorker> logger)
    {
        _client = client;
        _authenticationState = authenticationState;
        _runtimeStatus = runtimeStatus;
        _handlerRegistry = handlerRegistry;
        _executionStore = executionStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _authenticationState.WaitUntilReadyAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && _authenticationState.IsReady)
            {
                try
                {
                    var commands = await _client.GetPendingCommandsAsync(stoppingToken);
                    _runtimeStatus.MarkCommandPollSuccess();

                    foreach (var dto in commands)
                    {
                        await ProcessCommandAsync(dto, stoppingToken);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotImplemented)
                {
                    _runtimeStatus.MarkCommandPollSuccess();
                    _logger.LogDebug("Command endpoint'i henüz implemente edilmedi.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _runtimeStatus.MarkCommandPollFailure(ex.Message);
                    _logger.LogWarning(ex, "Komut kontrolü başarısız.");
                }

                if (!await DelayWhileAuthenticatedAsync(TimeSpan.FromSeconds(10), stoppingToken))
                    break;
            }
        }
    }

    private async Task ProcessCommandAsync(AgentCommandDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (dto.ExpiresAt.HasValue && DateTime.UtcNow > dto.ExpiresAt.Value)
            {
                _logger.LogWarning("Komut süresi dolmuş: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
                return;
            }

            if (_executionStore.HasExecuted(dto.IdempotencyKey))
            {
                _logger.LogInformation("Komut zaten çalıştırılmış (idempotent): {CommandType} ({CommandId})", dto.CommandType, dto.Id);
                await _client.AcceptCommandAsync(dto.Id, cancellationToken);
                var cached = _executionStore.GetResult(dto.IdempotencyKey);
                if (cached is not null && cached.Success)
                    await _client.CompleteCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = true, ResultPayload = cached.ResultPayload }, cancellationToken);
                return;
            }

            switch (dto.CommandType)
            {
                case "Ping":
                    await ExecuteTypedCommandAsync(dto, new PingCommand(), _handlerRegistry.Resolve<PingCommand>(dto.CommandType), cancellationToken);
                    break;
                case "HealthCheck":
                    await ExecuteTypedCommandAsync(dto, new HealthCheckCommand(), _handlerRegistry.Resolve<HealthCheckCommand>(dto.CommandType), cancellationToken);
                    break;
                case "RefreshConfiguration":
                    await ExecuteTypedCommandAsync(dto, new RefreshConfigurationCommand(), _handlerRegistry.Resolve<RefreshConfigurationCommand>(dto.CommandType), cancellationToken);
                    break;
                case "PavoPairing":
                    await ExecuteTypedCommandAsync(dto, DeserializeCommand<Modules.Pavo.Commands.PavoPairingCommand>(dto.Payload), _handlerRegistry.Resolve<Modules.Pavo.Commands.PavoPairingCommand>(dto.CommandType), cancellationToken);
                    break;
                case "PavoPing":
                    await ExecuteTypedCommandAsync(dto, DeserializeCommand<Modules.Pavo.Commands.PavoPingCommand>(dto.Payload), _handlerRegistry.Resolve<Modules.Pavo.Commands.PavoPingCommand>(dto.CommandType), cancellationToken);
                    break;
                case "PavoGetDeviceInfo":
                    await ExecuteTypedCommandAsync(dto, DeserializeCommand<Modules.Pavo.Commands.PavoGetDeviceInfoCommand>(dto.Payload), _handlerRegistry.Resolve<Modules.Pavo.Commands.PavoGetDeviceInfoCommand>(dto.CommandType), cancellationToken);
                    break;
                case "PavoStartPayment":
                    await ExecuteTypedCommandAsync(dto, DeserializeCommand<Modules.Pavo.Commands.PavoStartPaymentCommand>(dto.Payload), _handlerRegistry.Resolve<Modules.Pavo.Commands.PavoStartPaymentCommand>(dto.CommandType), cancellationToken);
                    break;
                case "PavoGetPaymentResult":
                    await ExecuteTypedCommandAsync(dto, DeserializeCommand<Modules.Pavo.Commands.PavoGetPaymentResultCommand>(dto.Payload), _handlerRegistry.Resolve<Modules.Pavo.Commands.PavoGetPaymentResultCommand>(dto.CommandType), cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Bilinmeyen komut tipi, rejected: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
                    await _client.RejectCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = false, ErrorMessage = $"Unknown command: {dto.CommandType}", ErrorCode = "UNKNOWN_COMMAND" }, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Komut işlenirken beklenmeyen hata: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
            try
            {
                await _client.FailCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = false, ErrorMessage = ex.Message, ErrorCode = "HANDLER_EXCEPTION" }, CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private async Task ExecuteTypedCommandAsync<TCommand>(
        AgentCommandDto dto,
        TCommand command,
        IAgentCommandHandler<TCommand>? handler,
        CancellationToken cancellationToken)
        where TCommand : IAgentCommand
    {
        if (handler is null)
        {
            throw new InvalidOperationException($"Komut handler bulunamadı: {dto.CommandType}");
        }

        _logger.LogInformation("Komut işleniyor: {CommandType} ({CommandId})", dto.CommandType, dto.Id);

        await _client.AcceptCommandAsync(dto.Id, cancellationToken);
        await _client.SetRunningCommandAsync(dto.Id, cancellationToken);
        _executionStore.MarkExecuted(dto.IdempotencyKey);

        var result = await handler.HandleAsync(command, cancellationToken);
        _executionStore.StoreResult(dto.IdempotencyKey, result);

        if (result.Success)
        {
            await _client.CompleteCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = true, ResultPayload = result.ResultPayload }, cancellationToken);
            _logger.LogInformation("Komut tamamlandı: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
        }
        else
        {
            await _client.CompleteCommandAsync(dto.Id, new AgentCommandCompleteRequest
            {
                Id = dto.Id,
                Success = false,
                ResultPayload = result.ResultPayload,
                ErrorMessage = result.ErrorMessage,
                ErrorCode = result.ErrorCode
            }, cancellationToken);
            _logger.LogWarning("Komut başarısız: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
        }
    }

    private async Task<bool> DelayWhileAuthenticatedAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero && !cancellationToken.IsCancellationRequested && _authenticationState.IsReady)
        {
            var slice = remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining;
            await Task.Delay(slice, cancellationToken);
            remaining -= slice;
        }

        return _authenticationState.IsReady && !cancellationToken.IsCancellationRequested;
    }

    private static TCommand DeserializeCommand<TCommand>(string? payload)
        where TCommand : IAgentCommand, new()
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new TCommand();

        return JsonSerializer.Deserialize<TCommand>(payload, JsonOptions) ?? new TCommand();
    }
}

internal sealed class UnknownCommand : IAgentCommand { public string CommandType => "Unknown"; }

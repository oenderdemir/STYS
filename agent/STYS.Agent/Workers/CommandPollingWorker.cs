using STYS.Agent.Client;
using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace STYS.Agent.Workers;

public sealed class CommandPollingWorker : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentCommandHandlerRegistry _handlerRegistry;
    private readonly IAgentCommandExecutionStore _executionStore;
    private readonly ILogger<CommandPollingWorker> _logger;

    public CommandPollingWorker(
        IStysAgentApiClient client,
        IAgentCommandHandlerRegistry handlerRegistry,
        IAgentCommandExecutionStore executionStore,
        ILogger<CommandPollingWorker> logger)
    {
        _client = client;
        _handlerRegistry = handlerRegistry;
        _executionStore = executionStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var commands = await _client.GetPendingCommandsAsync(stoppingToken);
                foreach (var dto in commands)
                {
                    await ProcessCommandAsync(dto, stoppingToken);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            {
                _logger.LogDebug("Command endpoint'i henüz implemente edilmedi.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Komut kontrolü başarısız.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessCommandAsync(AgentCommandDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (_executionStore.HasExecuted(dto.IdempotencyKey))
            {
                _logger.LogInformation("Komut zaten çalıştırılmış (idempotent): {CommandType} ({CommandId})", dto.CommandType, dto.Id);
                return;
            }

            var handler = _handlerRegistry.Resolve<IAgentCommand>(dto.CommandType);
            if (handler is null)
            {
                _logger.LogWarning("Bilinmeyen komut tipi: {CommandType}", dto.CommandType);
                return;
            }

            _logger.LogInformation("Komut işleniyor: {CommandType} ({CommandId})", dto.CommandType, dto.Id);

            await _client.AcceptCommandAsync(dto.Id, cancellationToken);

            var result = await handler.HandleAsync(DeserializeForHandler(dto.CommandType), cancellationToken);

            _executionStore.StoreResult(dto.IdempotencyKey, result);

            if (result.Success)
            {
                await _client.CompleteCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = true, ResultPayload = result.ResultPayload }, cancellationToken);
                _logger.LogInformation("Komut tamamlandı: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
            }
            else
            {
                await _client.FailCommandAsync(dto.Id, new AgentCommandCompleteRequest { Id = dto.Id, Success = false, ErrorMessage = result.ErrorMessage, ErrorCode = result.ErrorCode }, cancellationToken);
                _logger.LogWarning("Komut başarısız: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Komut işlenirken hata: {CommandType} ({CommandId})", dto.CommandType, dto.Id);
        }
    }

    private static IAgentCommand DeserializeForHandler(string commandType) => commandType switch
    {
        "Ping" => new PingCommand(),
        "HealthCheck" => new HealthCheckCommand(),
        "RefreshConfiguration" => new RefreshConfigurationCommand(),
        "PavoConnectionTest" => new Modules.Pavo.Commands.PavoConnectionTestCommand(),
        _ => new UnknownCommand()
    };
}

internal sealed class UnknownCommand : IAgentCommand { public string CommandType => "Unknown"; }

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Entegrasyonlar.Pos.Services;
using STYS.Infrastructure.EntityFramework;
using STYS.Entegrasyonlar.Pos.Entities;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Controllers;

[Route("api/agent")]
[ApiController]
public sealed class AgentAuthController : ControllerBase
{
    private readonly IAgentTokenService _tokenService;
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly AgentCommandService _commandService;
    private readonly IPosCihaziService _posCihaziService;
    private readonly IAgentRealtimeNotifier _realtimeNotifier;

    public AgentAuthController(
        IAgentTokenService tokenService,
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        AgentCommandService commandService,
        IPosCihaziService posCihaziService,
        IAgentRealtimeNotifier realtimeNotifier)
    {
        _tokenService = tokenService;
        _dbContextFactory = dbContextFactory;
        _commandService = commandService;
        _posCihaziService = posCihaziService;
        _realtimeNotifier = realtimeNotifier;
    }

    [HttpPost("enroll")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentEnrollmentResponse>> Enroll(
        [FromBody] AgentEnrollmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _tokenService.EnrollAsync(request, cancellationToken));

    [HttpPost("auth/token")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentTokenResponse>> GetToken(
        [FromBody] AgentTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _tokenService.IssueTokenAsync(request, cancellationToken));

    [HttpPost("heartbeat")]
    [Authorize(Policy = AgentPolicies.AgentHeartbeat)]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat(
        [FromBody] AgentHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated)
            return Unauthorized();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == agentContext.AgentId && !x.IsDeleted, cancellationToken);

        if (agent is not null)
        {
            agent.LastHeartbeatAt = DateTime.UtcNow;
            agent.SonGorulmeTarihi = DateTime.UtcNow;
            agent.AgentVersion = request.AgentVersion;
            agent.CihazKimligi ??= request.CihazKimligi;
            await db.SaveChangesAsync(cancellationToken);
            await _realtimeNotifier.AgentChangedAsync(cancellationToken);
        }

        return Ok(new AgentHeartbeatResponse
        {
            RequiredUpdate = false
        });
    }

    [HttpGet("config")]
    [Authorize(Policy = AgentPolicies.AgentConfigRead)]
    public async Task<ActionResult<AgentConfigDto>> GetConfig(
        [FromQuery] long currentVersion,
        CancellationToken cancellationToken)
    {
        return Ok(new AgentConfigDto
        {
            Version = 1,
            Configs = new Dictionary<string, string>
            {
                ["heartbeatIntervalSeconds"] = "30",
                ["commandPollIntervalSeconds"] = "10",
                ["maxRetryCount"] = "3"
            }
        });
    }

    [HttpGet("me")]
    [Authorize(Policy = AgentPolicies.AgentPolicy)]
    public async Task<ActionResult<AgentSelfDto>> Me(CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated)
            return Unauthorized();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .Include(x => x.Tesisler)
            .Include(x => x.Scopes)
            .FirstOrDefaultAsync(x => x.Id == agentContext.AgentId && !x.IsDeleted, cancellationToken);

        if (agent is null)
            return NotFound();

        var capabilities = await db.Set<AgentCapability>()
            .Where(x => x.AgentId == agent.Id && x.AktifMi && !x.IsDeleted)
            .Select(x => x.Capability)
            .ToListAsync(cancellationToken);

        var kurumAd = await db.Set<STYS.Kurumlar.Entities.Kurum>()
            .Where(x => x.Id == agent.KurumId && !x.IsDeleted)
            .Select(x => x.Ad)
            .FirstOrDefaultAsync(cancellationToken);

        var tesisler = await db.Set<STYS.Tesisler.Entities.Tesis>()
            .Where(x => agent.Tesisler.Select(t => t.TesisId).Contains(x.Id) && !x.IsDeleted)
            .Select(x => new AgentSelfTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        if (tesisler.Count == 0)
        {
            tesisler = agent.Tesisler
                .Where(x => !x.IsDeleted)
                .Select(x => new AgentSelfTesisDto
                {
                    Id = x.TesisId,
                    Ad = x.TesisId.ToString()
                })
                .ToList();
        }

        return Ok(new AgentSelfDto
        {
            AgentId = agent.Id,
            AgentAd = agent.Ad,
            AgentKey = agent.AgentKey,
            KurumId = agent.KurumId,
            KurumAd = kurumAd,
            Tesisler = tesisler,
            Scopes = agent.Scopes
                .Where(x => !x.IsDeleted && x.AktifMi)
                .Select(x => x.Scope)
                .ToList(),
            Capabilities = capabilities,
            Durum = (int)agent.Durum,
            AgentVersion = agent.AgentVersion,
            LastHeartbeatAt = agent.LastHeartbeatAt,
            OnlineMi = agent.LastHeartbeatAt.HasValue && (DateTime.UtcNow - agent.LastHeartbeatAt.Value) <= TimeSpan.FromSeconds(90)
        });
    }

    [HttpPost("pos-devices/register")]
    [Authorize(Policy = AgentPolicies.AgentPolicy)]
    public async Task<ActionResult<AgentPavoDeviceRegistrationResult>> RegisterPosDevice(
        [FromBody] AgentPavoDeviceRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated)
            return Unauthorized();

        return Ok(await _posCihaziService.RegisterFromAgentAsync(request, cancellationToken));
    }

    [HttpPost("pos-devices/status-snapshot")]
    [Authorize(Policy = AgentPolicies.AgentPolicy)]
    public async Task<ActionResult<AgentPavoDeviceStatusSnapshotDto?>> GetPosDeviceStatusSnapshot(
        [FromBody] AgentPavoDeviceStatusSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated)
            return Unauthorized();

        if (request is null)
            return BadRequest(new { message = "İstek zorunludur." });

        if (!string.Equals(request.Provider?.Trim(), "PAVO", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Sadece PAVO provider destekleniyor." });

        if (string.IsNullOrWhiteSpace(request.LocalDeviceId))
            return BadRequest(new { message = "Local cihaz kimliği zorunludur." });

        if (string.IsNullOrWhiteSpace(request.SerialNumber))
            return BadRequest(new { message = "Seri numarası zorunludur." });

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var devices = await db.PosCihazlari
            .AsNoTracking()
            .Include(x => x.Terminaller.Where(t => !t.IsDeleted))
            .Where(x =>
                !x.IsDeleted
                && x.KurumId == agentContext.KurumId
                && x.AgentId == agentContext.AgentId
                && x.Saglayici == PosSaglayici.Pavo)
            .ToListAsync(cancellationToken);

        var device = devices
            .Where(x =>
                string.Equals(x.AgentLocalDeviceId, request.LocalDeviceId.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.SeriNo, request.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => string.Equals(x.AgentLocalDeviceId, request.LocalDeviceId.Trim(), StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => string.Equals(x.SeriNo, request.SerialNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (device is null)
        {
            return Ok(null);
        }

        return Ok(new AgentPavoDeviceStatusSnapshotDto
        {
            CentralPosCihaziId = device.Id,
            AgentId = device.AgentId,
            KurumId = device.KurumId,
            TesisId = device.TesisId,
            AgentLocalDeviceId = device.AgentLocalDeviceId,
            Provider = "PAVO",
            SerialNumber = device.SeriNo,
            Active = device.AktifMi,
            DisplayName = device.Ad,
            Host = device.IpAdresi,
            HttpPort = device.HttpPort,
            HttpsPort = device.HttpsPort,
            Fingerprint = device.Fingerprint,
            TargetFingerprint = device.TargetFingerprint,
            SonBaglantiTarihi = device.SonBaglantiTarihi,
            Terminals = device.Terminaller
                .Where(x => !x.IsDeleted)
                .Select(x => new PavoDeviceProvisioningCandidateTerminal
                {
                    AcquirerId = x.AcquirerId,
                    AcquirerName = x.AcquirerName,
                    TerminalId = x.CanonicalTerminalId ?? x.SerialNumber,
                    MerchantId = x.SourceTerminalReference,
                    SourceReference = x.SourceTerminalReference ?? x.CanonicalTerminalId,
                    Active = x.AktifMi
                })
                .ToList()
        });
    }

    [HttpGet("commands")]
    [Authorize(Policy = AgentPolicies.AgentCommandRead)]
    public async Task<ActionResult<IReadOnlyCollection<AgentCommandDto>>> GetPendingCommands(CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        return Ok(await _commandService.GetPendingCommandsAsync(agentContext.AgentId, cancellationToken));
    }

    [HttpPost("commands/{id:guid}/accept")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> AcceptCommand(Guid id, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.AcceptAsync(id, agentContext.AgentId, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/running")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> SetRunningCommand(Guid id, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.SetRunningAsync(id, agentContext.AgentId, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/complete")]
    [Authorize(Policy = AgentPolicies.AgentResultWrite)]
    public async Task<ActionResult> CompleteCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.CompleteAsync(id, agentContext.AgentId, request, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/fail")]
    [Authorize(Policy = AgentPolicies.AgentResultWrite)]
    public async Task<ActionResult> FailCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.FailAsync(id, agentContext.AgentId, request.ErrorMessage ?? "Unknown error", cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/reject")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> RejectCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.RejectAsync(id, agentContext.AgentId, request.ErrorMessage ?? "Unknown command", cancellationToken);
        return Ok();
    }
}

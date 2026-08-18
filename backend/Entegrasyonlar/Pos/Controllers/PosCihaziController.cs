using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Contracts.Dtos;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pos.Controllers;

[Route("ui/pos")]
public sealed class PosCihaziController : UIController
{
    private readonly IPosCihaziService _service;
    private readonly IPosPaymentTestService _paymentService;
    private readonly IPosReceiptService _receiptService;
    private readonly IPosGunSonuService _gunSonuService;
    private readonly IMapper _mapper;

    public PosCihaziController(
        IPosCihaziService service,
        IPosPaymentTestService paymentService,
        IPosReceiptService receiptService,
        IPosGunSonuService gunSonuService,
        IMapper mapper)
    {
        _service = service;
        _paymentService = paymentService;
        _receiptService = receiptService;
        _gunSonuService = gunSonuService;
        _mapper = mapper;
    }

    [HttpGet("cihazlar")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IEnumerable<PosCihaziDto>>> GetAll(
        [FromQuery] int? kurumId,
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(kurumId, tesisId, cancellationToken));

    [HttpGet("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<PosCihaziDto>> GetById(int id) => Ok(await _service.GetByIdAsync(id));

    [HttpGet("cihazlar/{id:int}/readiness")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<PosOperationalReadinessDto>> GetReadiness(int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetReadinessAsync(id, cancellationToken));

    [HttpPost("cihazlar")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Create([FromBody] PosCihaziKaydetRequest req)
    {
        var dto = _mapper.Map<PosCihaziDto>(req);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Update(int id, [FromBody] PosCihaziKaydetRequest req)
    {
        var dto = _mapper.Map<PosCihaziDto>(req);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult> Delete(int id) { await _service.DeleteAsync(id); return Ok(); }

    [HttpPost("cihazlar/{id:int}/pairing")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> Pairing(int id, CancellationToken cancellationToken) =>
        Ok(await _service.PairingAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("cihazlar/{id:int}/ping")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> Ping(int id, CancellationToken cancellationToken) =>
        Ok(await _service.PingAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("cihazlar/{id:int}/device-info")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> GetDeviceInfo(int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetDeviceInfoAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("cihazlar/{id:int}/terminal-discovery")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> TerminalDiscovery(int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetDeviceInfoAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("cihazlar/{id:int}/payment-test")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<PosOdemeIslemiDto>>> GetPaymentTests(
        int id,
        CancellationToken cancellationToken,
        [FromQuery] int take = 5) =>
        Ok(await _paymentService.GetRecentAsync(id, take, cancellationToken));

    [HttpPost("cihazlar/{id:int}/payment-test")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto>> StartPaymentTest(
        int id,
        [FromBody] PosPaymentBaslatRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _paymentService.StartAsync(id, request, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("cihazlar/{id:int}/payment-test/{posOdemeIslemiId:int}/result")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto>> GetPaymentResult(
        int id,
        int posOdemeIslemiId,
        CancellationToken cancellationToken) =>
        Ok(await _paymentService.GetResultAsync(id, posOdemeIslemiId, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("cihazlar/{id:int}/payment-test/{posOdemeIslemiId:int}/receipts/recover")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto>> RecoverPaymentReceipts(
        int id,
        int posOdemeIslemiId,
        CancellationToken cancellationToken) =>
        Ok(await _paymentService.RecoverReceiptsAsync(id, posOdemeIslemiId, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("payments/{paymentId:int}/receipts")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<PosOdemeSlipDto>>> GetReceipts(
        int paymentId,
        CancellationToken cancellationToken) =>
        Ok(await _receiptService.GetReceiptsAsync(paymentId, cancellationToken));

    [HttpGet("payments/{paymentId:int}/receipts/{receiptId:int}/content")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<IActionResult> GetReceiptContent(
        int paymentId,
        int receiptId,
        CancellationToken cancellationToken)
    {
        var content = await _receiptService.OpenReceiptContentAsync(paymentId, receiptId, cancellationToken);
        return File(content.Stream, content.ContentType);
    }

    [HttpPost("cihazlar/{id:int}/eod")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosGunSonuIslemiDto>> StartEod(
        int id,
        [FromBody] PosGunSonuBaslatRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _gunSonuService.PerformAsync(id, request ?? new PosGunSonuBaslatRequest(), User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("cihazlar/{id:int}/eod")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<PosGunSonuIslemiDto>>> GetEodHistory(
        int id,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await _gunSonuService.GetRecentAsync(id, take, cancellationToken));

    [HttpGet("eod/{eodId:int}")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<PosGunSonuIslemiDetayDto>> GetEodDetail(int eodId, CancellationToken cancellationToken) =>
        Ok(await _gunSonuService.GetByIdAsync(eodId, cancellationToken));

    [HttpGet("eod/{eodId:int}/receipts")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<PosGunSonuSlipiDto>>> GetEodReceipts(int eodId, CancellationToken cancellationToken) =>
        Ok(await _gunSonuService.GetSliplerAsync(eodId, cancellationToken));

    [HttpGet("eod/{eodId:int}/receipts/{receiptId:int}/content")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<IActionResult> GetEodReceiptContent(int eodId, int receiptId, CancellationToken cancellationToken)
    {
        var content = await _gunSonuService.OpenSlipContentAsync(eodId, receiptId, cancellationToken);
        return File(content.Stream, content.ContentType);
    }
}

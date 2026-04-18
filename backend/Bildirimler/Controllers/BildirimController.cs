using Microsoft.AspNetCore.Mvc;
using STYS.Bildirimler.Dto;
using STYS.Bildirimler.Services;
using STYS.Licensing;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Bildirimler.Controllers;

[RequiresLicensedModule(StysLicensedModules.Bildirim)]
public class BildirimController : UIController
{
    private readonly IBildirimService _bildirimService;

    public BildirimController(IBildirimService bildirimService)
    {
        _bildirimService = bildirimService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BildirimDto>>> GetLatest([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var result = await _bildirimService.GetCurrentUserBildirimlerAsync(take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var count = await _bildirimService.GetCurrentUserUnreadCountAsync(cancellationToken);
        return Ok(count);
    }

    [HttpGet("tercih")]
    public async Task<ActionResult<BildirimTercihDto>> GetTercih(CancellationToken cancellationToken = default)
    {
        var result = await _bildirimService.GetCurrentUserTercihAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("tercih")]
    public async Task<ActionResult<BildirimTercihDto>> UpdateTercih(
        [FromBody] BildirimTercihGuncelleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _bildirimService.UpdateCurrentUserTercihAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{bildirimId:int}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] int bildirimId, CancellationToken cancellationToken = default)
    {
        await _bildirimService.MarkAsReadAsync(bildirimId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        await _bildirimService.MarkAllAsReadAsync(cancellationToken);
        return NoContent();
    }
}

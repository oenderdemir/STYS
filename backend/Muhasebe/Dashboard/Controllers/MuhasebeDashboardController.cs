using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Dashboard.Dtos;
using STYS.Muhasebe.Dashboard.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.Dashboard.Controllers;

[Route("ui/muhasebe/dashboard")]
public class MuhasebeDashboardController : UIController
{
    private readonly IMuhasebeDashboardService _service;

    public MuhasebeDashboardController(IMuhasebeDashboardService service)
    {
        _service = service;
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeDashboardYonetimi.View)]
    public async Task<IActionResult> GetDashboard(
        [FromBody] MuhasebeDashboardFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetDashboardAsync(filter, cancellationToken);
        return Ok(result);
    }
}

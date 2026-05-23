using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.Kdv.Controllers;

[Route("ui/muhasebe/kdv-ozet-raporu")]
public class KdvOzetRaporController : UIController
{
    private readonly IKdvOzetRaporService _service;

    public KdvOzetRaporController(IKdvOzetRaporService service)
    {
        _service = service;
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<IActionResult> GetOzetRapor(
        [FromBody] KdvOzetRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetOzetRaporAsync(filter, cancellationToken);
        return Ok(result);
    }
}

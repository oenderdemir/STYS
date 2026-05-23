using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.Kdv.Controllers;

[Route("ui/muhasebe/kdv-hareket-raporu")]
public class KdvHareketRaporController : UIController
{
    private readonly IKdvHareketRaporService _service;

    public KdvHareketRaporController(IKdvHareketRaporService service)
    {
        _service = service;
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<IActionResult> GetRapor(
        [FromBody] KdvHareketRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetRaporAsync(filter, cancellationToken);
        return Ok(result);
    }
}

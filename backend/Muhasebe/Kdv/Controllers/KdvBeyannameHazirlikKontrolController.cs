using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.Kdv.Controllers;

[Route("ui/muhasebe/kdv-beyanname-hazirlik-kontrol")]
public class KdvBeyannameHazirlikKontrolController : UIController
{
    private readonly IKdvBeyannameHazirlikKontrolService _service;

    public KdvBeyannameHazirlikKontrolController(IKdvBeyannameHazirlikKontrolService service)
    {
        _service = service;
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<IActionResult> KontrolEt(
        [FromBody] KdvBeyannameHazirlikKontrolFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.KontrolEtAsync(filter, cancellationToken);
        return Ok(result);
    }
}

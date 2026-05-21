using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.DonemKapanis.Dtos;
using STYS.Muhasebe.DonemKapanis.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.DonemKapanis.Controllers;

[Route("ui/muhasebe/donem-kapanis")]
public class DonemKapanisKontrolController : UIController
{
    private readonly IDonemKapanisKontrolService _service;

    public DonemKapanisKontrolController(IDonemKapanisKontrolService service)
    {
        _service = service;
    }

    [HttpPost("kontrol")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.View)]
    public async Task<IActionResult> KontrolEt(
        [FromBody] DonemKapanisKontrolFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.KontrolEtAsync(filter, cancellationToken);
        return Ok(result);
    }
}

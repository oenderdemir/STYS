using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.Dto;
using STYS.Raporlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.Controllers;

[Route("api/raporlar")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OdaDolulukRaporController : UIController
{
    private readonly IOdaDolulukRaporService _odaDolulukRaporService;

    public OdaDolulukRaporController(IOdaDolulukRaporService odaDolulukRaporService)
    {
        _odaDolulukRaporService = odaDolulukRaporService;
    }

    [HttpGet("oda-doluluk-aylik")]
    [Permission(StructurePermissions.OdaDolulukRaporuYonetimi.View)]
    public async Task<ActionResult<AylikOdaDolulukRaporDto>> GetAylikOdaDolulukRaporu(
        [FromQuery] int tesisId,
        [FromQuery] int yil,
        [FromQuery] int ay,
        [FromQuery] bool maskele,
        CancellationToken cancellationToken)
    {
        var rapor = await _odaDolulukRaporService.GetAylikOdaDolulukRaporuAsync(tesisId, yil, ay, maskele, cancellationToken);
        return Ok(rapor);
    }

    // TODO: GET api/raporlar/oda-doluluk-aylik/excel - Excel export endpointi eklenecek.
    // TODO: GET api/raporlar/oda-doluluk-aylik/pdf - PDF export endpointi eklenecek.
}

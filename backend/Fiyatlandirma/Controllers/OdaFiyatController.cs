using Microsoft.AspNetCore.Mvc;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Fiyatlandirma.Controllers;

public class OdaFiyatController : UIController
{
    private readonly IOdaFiyatService _odaFiyatService;

    public OdaFiyatController(IOdaFiyatService odaFiyatService)
    {
        _odaFiyatService = odaFiyatService;
    }

    [HttpGet("odatipi/{tesisOdaTipiId:int}")]
    [Permission(StructurePermissions.OdaFiyatYonetimi.View)]
    public async Task<ActionResult<List<OdaFiyatDto>>> GetByTesisOdaTipiId(int tesisOdaTipiId, CancellationToken cancellationToken)
    {
        var items = await _odaFiyatService.GetByTesisOdaTipiIdAsync(tesisOdaTipiId, cancellationToken);
        return Ok(items);
    }

    [HttpPut("odatipi/{tesisOdaTipiId:int}")]
    [Permission(StructurePermissions.OdaFiyatYonetimi.Manage)]
    public async Task<ActionResult<List<OdaFiyatDto>>> UpsertByTesisOdaTipi(int tesisOdaTipiId, [FromBody] List<OdaFiyatDto> fiyatlar, CancellationToken cancellationToken)
    {
        var result = await _odaFiyatService.UpsertByTesisOdaTipiAsync(tesisOdaTipiId, fiyatlar, cancellationToken);
        return Ok(result);
    }

    [HttpPost("hesapla")]
    [Permission(StructurePermissions.OdaFiyatYonetimi.View)]
    public async Task<ActionResult<OdaFiyatHesaplamaSonucuDto>> Hesapla([FromBody] OdaFiyatHesaplaRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _odaFiyatService.HesaplaAsync(request, cancellationToken);
        return Ok(result);
    }
}

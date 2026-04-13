using Microsoft.AspNetCore.Mvc;
using STYS.GarsonServis.Dtos;
using STYS.GarsonServis.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.GarsonServis.Controllers;

[Route("api/garson")]
[ApiController]
public class GarsonServisController : UIController
{
    private readonly IGarsonServisService _service;

    public GarsonServisController(IGarsonServisService service)
    {
        _service = service;
    }

    [HttpGet("restoranlar/{restoranId:int}/masalar")]
    [Permission(StructurePermissions.GarsonServisYonetimi.View)]
    public async Task<ActionResult<List<GarsonMasaDto>>> GetMasalar(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetMasalarAsync(restoranId, cancellationToken));

    [HttpGet("masalar/{masaId:int}/oturum")]
    [Permission(StructurePermissions.GarsonServisYonetimi.View)]
    public async Task<ActionResult<MasaOturumuDto>> GetMasaOturumu(int masaId, CancellationToken cancellationToken)
    {
        var oturum = await _service.GetMasaOturumuByMasaAsync(masaId, cancellationToken);
        return oturum is null ? NotFound() : Ok(oturum);
    }

    [HttpPost("masalar/{masaId:int}/oturum")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> StartOrGetMasaOturumu(int masaId, [FromBody] CreateMasaOturumuRequest request, CancellationToken cancellationToken)
        => Ok(await _service.StartOrGetMasaOturumuAsync(masaId, request, cancellationToken));

    [HttpPost("oturumlar/{oturumId:int}/kalemler")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> AddKalem(int oturumId, [FromBody] AddMasaOturumuKalemiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddKalemAsync(oturumId, request, cancellationToken));

    [HttpPut("oturumlar/{oturumId:int}/kalemler/{kalemId:int}")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> UpdateKalem(int oturumId, int kalemId, [FromBody] UpdateMasaOturumuKalemiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateKalemAsync(oturumId, kalemId, request, cancellationToken));

    [HttpDelete("oturumlar/{oturumId:int}/kalemler/{kalemId:int}")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> DeleteKalem(int oturumId, int kalemId, CancellationToken cancellationToken)
        => Ok(await _service.DeleteKalemAsync(oturumId, kalemId, cancellationToken));

    [HttpPut("oturumlar/{oturumId:int}/not")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> UpdateNot(int oturumId, [FromBody] UpdateMasaOturumuNotRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateNotAsync(oturumId, request, cancellationToken));

    [HttpPut("oturumlar/{oturumId:int}/durum")]
    [Permission(StructurePermissions.GarsonServisYonetimi.Manage)]
    public async Task<ActionResult<MasaOturumuDto>> UpdateDurum(int oturumId, [FromBody] UpdateMasaOturumuDurumRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateDurumAsync(oturumId, request, cancellationToken));

    [HttpGet("restoranlar/{restoranId:int}/menu")]
    [Permission(StructurePermissions.GarsonServisYonetimi.View)]
    public async Task<ActionResult<GarsonMenuDto>> GetMenu(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetMenuAsync(restoranId, cancellationToken));
}

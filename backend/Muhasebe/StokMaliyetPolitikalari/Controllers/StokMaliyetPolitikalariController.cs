using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Controllers;

[Route("ui/muhasebe/stok-maliyet-politikalari")]
public class StokMaliyetPolitikalariController : UIController
{
    private readonly IStokMaliyetPolitikasiService _service;

    public StokMaliyetPolitikalariController(IStokMaliyetPolitikasiService service)
    {
        _service = service;
    }

    [HttpGet("current")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<CurrentStokMaliyetPolitikasiDto>> GetCurrent([FromQuery] int tesisId, [FromQuery] DateTime? tarih, CancellationToken cancellationToken)
        => Ok(await _service.GetCurrentAsync(tesisId, tarih ?? DateTime.UtcNow, cancellationToken));

    [HttpGet]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<StokMaliyetPolitikasiDto>> GetByTesisMaliYil([FromQuery] int tesisId, [FromQuery] int maliYil, CancellationToken cancellationToken)
    {
        var item = await _service.GetByTesisMaliYilAsync(tesisId, maliYil, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokMaliyetPolitikasiDto>> Upsert([FromBody] UpsertStokMaliyetPolitikasiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpsertAsync(request, cancellationToken));
}

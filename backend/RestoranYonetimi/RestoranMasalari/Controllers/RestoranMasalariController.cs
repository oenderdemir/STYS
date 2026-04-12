using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranMasalari.Controllers;

[Route("api/restoran-masalari")]
[ApiController]
public class RestoranMasalariController : UIController
{
    private readonly IRestoranMasaService _service;

    public RestoranMasalariController(IRestoranMasaService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranMasaYonetimi.View)]
    public async Task<ActionResult<List<RestoranMasaDto>>> GetList([FromQuery] int? restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(restoranId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.View)]
    public async Task<ActionResult<RestoranMasaDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<ActionResult<RestoranMasaDto>> Create([FromBody] CreateRestoranMasaRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<ActionResult<RestoranMasaDto>> Update(int id, [FromBody] UpdateRestoranMasaRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("/api/restoranlar/{restoranId:int}/masalar")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.View)]
    public async Task<ActionResult<List<RestoranMasaDto>>> GetByRestoranId(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetByRestoranIdAsync(restoranId, cancellationToken));
}

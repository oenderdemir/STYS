using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMasalari.Dtos;
using STYS.RestoranMasalari.Services;
using System.Linq;
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
    {
        var items = restoranId.HasValue && restoranId.Value > 0
            ? await _service.WhereAsync(x => x.RestoranId == restoranId.Value)
            : await _service.GetAllAsync();
        return Ok(items.ToList());
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.View)]
    public async Task<ActionResult<RestoranMasaDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<ActionResult<RestoranMasaDto>> Create([FromBody] CreateRestoranMasaRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMasaDto
        {
            RestoranId = request.RestoranId,
            MasaNo = request.MasaNo,
            Kapasite = request.Kapasite,
            Durum = request.Durum,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<ActionResult<RestoranMasaDto>> Update(int id, [FromBody] UpdateRestoranMasaRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMasaDto
        {
            Id = id,
            RestoranId = request.RestoranId,
            MasaNo = request.MasaNo,
            Kapasite = request.Kapasite,
            Durum = request.Durum,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpGet("/api/restoranlar/{restoranId:int}/masalar")]
    [Permission(StructurePermissions.RestoranMasaYonetimi.View)]
    public async Task<ActionResult<List<RestoranMasaDto>>> GetByRestoranId(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetByRestoranIdAsync(restoranId, cancellationToken));
}

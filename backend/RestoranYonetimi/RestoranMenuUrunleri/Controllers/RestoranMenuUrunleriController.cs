using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMenuUrunleri.Dtos;
using STYS.RestoranMenuUrunleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranMenuUrunleri.Controllers;

[Route("api/restoran-menu-urunleri")]
[ApiController]
public class RestoranMenuUrunleriController : UIController
{
    private readonly IRestoranMenuUrunService _service;

    public RestoranMenuUrunleriController(IRestoranMenuUrunService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<List<RestoranMenuUrunDto>>> GetList([FromQuery] int? kategoriId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(kategoriId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuUrunDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuUrunDto>> Create([FromBody] CreateRestoranMenuUrunRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuUrunDto>> Update(int id, [FromBody] UpdateRestoranMenuUrunRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }
}

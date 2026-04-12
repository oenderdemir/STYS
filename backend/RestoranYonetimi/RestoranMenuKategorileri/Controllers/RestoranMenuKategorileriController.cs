using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMenuKategorileri.Dtos;
using STYS.RestoranMenuKategorileri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranMenuKategorileri.Controllers;

[Route("api/restoran-menu-kategorileri")]
[ApiController]
public class RestoranMenuKategorileriController : UIController
{
    private readonly IRestoranMenuKategoriService _service;

    public RestoranMenuKategorileriController(IRestoranMenuKategoriService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<List<RestoranMenuKategoriDto>>> GetList([FromQuery] int? restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(restoranId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> Create([FromBody] CreateRestoranMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> Update(int id, [FromBody] UpdateRestoranMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("/api/restoranlar/{restoranId:int}/menu")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuDto>> GetMenuByRestoran(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetMenuByRestoranIdAsync(restoranId, cancellationToken));

    [HttpGet("global")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<List<RestoranGlobalMenuKategoriDto>>> GetGlobalList(CancellationToken cancellationToken)
        => Ok(await _service.GetGlobalListAsync(cancellationToken));

    [HttpPost("global")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranGlobalMenuKategoriDto>> CreateGlobal([FromBody] CreateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateGlobalAsync(request, cancellationToken));

    [HttpPut("global/{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranGlobalMenuKategoriDto>> UpdateGlobal(int id, [FromBody] UpdateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateGlobalAsync(id, request, cancellationToken));

    [HttpDelete("global/{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<IActionResult> DeleteGlobal(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteGlobalAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("atama-baglam")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranKategoriAtamaBaglamDto>> GetAtamaBaglam([FromQuery] int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetAtamaBaglamAsync(restoranId, cancellationToken));

    [HttpPut("atamalar")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranKategoriAtamaBaglamDto>> SaveAtamalar([FromBody] SaveRestoranKategoriAtamaRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SaveAtamalarAsync(request, cancellationToken));
}

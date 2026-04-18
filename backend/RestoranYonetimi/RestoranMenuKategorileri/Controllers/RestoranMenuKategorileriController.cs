using Microsoft.AspNetCore.Mvc;
using STYS.RestoranMenuKategorileri.Dtos;
using STYS.RestoranMenuKategorileri.Services;
using System.Linq;
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
    {
        var items = restoranId.HasValue && restoranId.Value > 0
            ? await _service.WhereAsync(x => x.RestoranId == restoranId.Value)
            : await _service.GetAllAsync();
        return Ok(items.ToList());
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> Create([FromBody] CreateRestoranMenuKategoriRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMenuKategoriDto
        {
            RestoranId = request.RestoranId,
            Ad = request.Ad,
            SiraNo = request.SiraNo,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<ActionResult<RestoranMenuKategoriDto>> Update(int id, [FromBody] UpdateRestoranMenuKategoriRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranMenuKategoriDto
        {
            Id = id,
            RestoranId = request.RestoranId,
            Ad = request.Ad,
            SiraNo = request.SiraNo,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpGet("/api/restoranlar/{restoranId:int}/menu")]
    [Permission(StructurePermissions.RestoranMenuYonetimi.View)]
    public async Task<ActionResult<RestoranMenuDto>> GetMenuByRestoran(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetMenuByRestoranIdAsync(restoranId, cancellationToken));

    [HttpGet("global")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.View)]
    public async Task<ActionResult<List<RestoranGlobalMenuKategoriDto>>> GetGlobalList(CancellationToken cancellationToken)
        => Ok(await _service.GetGlobalListAsync(cancellationToken));

    [HttpPost("global")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.Manage)]
    public async Task<ActionResult<RestoranGlobalMenuKategoriDto>> CreateGlobal([FromBody] CreateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateGlobalAsync(request, cancellationToken));

    [HttpPut("global/{id:int}")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.Manage)]
    public async Task<ActionResult<RestoranGlobalMenuKategoriDto>> UpdateGlobal(int id, [FromBody] UpdateRestoranGlobalMenuKategoriRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateGlobalAsync(id, request, cancellationToken));

    [HttpDelete("global/{id:int}")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.Manage)]
    public async Task<IActionResult> DeleteGlobal(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteGlobalAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("atama-baglam")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.View)]
    public async Task<ActionResult<RestoranKategoriAtamaBaglamDto>> GetAtamaBaglam([FromQuery] int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetAtamaBaglamAsync(restoranId, cancellationToken));

    [HttpPut("atamalar")]
    [Permission(StructurePermissions.RestoranKategoriHavuzuYonetimi.Manage)]
    public async Task<ActionResult<RestoranKategoriAtamaBaglamDto>> SaveAtamalar([FromBody] SaveRestoranKategoriAtamaRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SaveAtamalarAsync(request, cancellationToken));
}

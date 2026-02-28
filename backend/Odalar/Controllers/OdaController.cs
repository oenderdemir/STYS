using Microsoft.AspNetCore.Mvc;
using STYS.Odalar.Dto;
using STYS.Odalar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Odalar.Controllers;

public class OdaController : UIController
{
    private readonly IOdaService _odaService;

    public OdaController(IOdaService odaService)
    {
        _odaService = odaService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<List<OdaDto>> GetAll()
    {
        var odalar = await _odaService.GetAllAsync();
        return odalar.OrderBy(x => x.OdaNo).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _odaService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery) ? null : x => x.OdaNo.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.OdaNo));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<ActionResult<OdaDto>> GetById(int id)
    {
        var oda = await _odaService.GetByIdAsync(id);
        if (oda is null)
        {
            return NotFound();
        }

        return Ok(oda);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<ActionResult<OdaDto>> Create([FromBody] OdaDto dto)
    {
        var created = await _odaService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<ActionResult<OdaDto>> Update(int id, [FromBody] OdaDto dto)
    {
        dto.Id = id;
        var updated = await _odaService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaService.DeleteAsync(id);
        return Ok();
    }
}

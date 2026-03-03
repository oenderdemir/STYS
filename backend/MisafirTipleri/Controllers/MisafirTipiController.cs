using Microsoft.AspNetCore.Mvc;
using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.MisafirTipleri.Controllers;

public class MisafirTipiController : UIController
{
    private readonly IMisafirTipiService _misafirTipiService;

    public MisafirTipiController(IMisafirTipiService misafirTipiService)
    {
        _misafirTipiService = misafirTipiService;
    }

    [HttpGet]
    [Permission(StructurePermissions.MisafirTipiYonetimi.View)]
    public async Task<List<MisafirTipiDto>> GetAll()
    {
        var items = await _misafirTipiService.GetAllAsync();
        return items.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.MisafirTipiYonetimi.View)]
    public async Task<ActionResult<PagedResult<MisafirTipiDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _misafirTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MisafirTipiYonetimi.View)]
    public async Task<ActionResult<MisafirTipiDto>> GetById(int id)
    {
        var item = await _misafirTipiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MisafirTipiYonetimi.Manage)]
    public async Task<ActionResult<MisafirTipiDto>> Create([FromBody] MisafirTipiDto dto)
    {
        var created = await _misafirTipiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MisafirTipiYonetimi.Manage)]
    public async Task<ActionResult<MisafirTipiDto>> Update(int id, [FromBody] MisafirTipiDto dto)
    {
        dto.Id = id;
        var updated = await _misafirTipiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MisafirTipiYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _misafirTipiService.DeleteAsync(id);
        return Ok();
    }
}

using Microsoft.AspNetCore.Mvc;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Fiyatlandirma.Controllers;

public class IndirimKuraliController : UIController
{
    private readonly IIndirimKuraliService _indirimKuraliService;

    public IndirimKuraliController(IIndirimKuraliService indirimKuraliService)
    {
        _indirimKuraliService = indirimKuraliService;
    }

    [HttpGet]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<List<IndirimKuraliDto>> GetAll()
    {
        var items = await _indirimKuraliService.GetAllAsync();
        return items.OrderByDescending(x => x.Oncelik).ThenBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<ActionResult<PagedResult<IndirimKuraliDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _indirimKuraliService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: q => q.OrderByDescending(x => x.Oncelik).ThenBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<ActionResult<IndirimKuraliDto>> GetById(int id)
    {
        var item = await _indirimKuraliService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<ActionResult<IndirimKuraliDto>> Create([FromBody] IndirimKuraliDto dto)
    {
        var created = await _indirimKuraliService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<ActionResult<IndirimKuraliDto>> Update(int id, [FromBody] IndirimKuraliDto dto)
    {
        dto.Id = id;
        var updated = await _indirimKuraliService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _indirimKuraliService.DeleteAsync(id);
        return Ok();
    }
}

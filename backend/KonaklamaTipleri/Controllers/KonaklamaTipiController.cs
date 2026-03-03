using Microsoft.AspNetCore.Mvc;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.KonaklamaTipleri.Controllers;

public class KonaklamaTipiController : UIController
{
    private readonly IKonaklamaTipiService _konaklamaTipiService;

    public KonaklamaTipiController(IKonaklamaTipiService konaklamaTipiService)
    {
        _konaklamaTipiService = konaklamaTipiService;
    }

    [HttpGet]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.View)]
    public async Task<List<KonaklamaTipiDto>> GetAll()
    {
        var items = await _konaklamaTipiService.GetAllAsync();
        return items.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.View)]
    public async Task<ActionResult<PagedResult<KonaklamaTipiDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _konaklamaTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.View)]
    public async Task<ActionResult<KonaklamaTipiDto>> GetById(int id)
    {
        var item = await _konaklamaTipiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaTipiDto>> Create([FromBody] KonaklamaTipiDto dto)
    {
        var created = await _konaklamaTipiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaTipiDto>> Update(int id, [FromBody] KonaklamaTipiDto dto)
    {
        dto.Id = id;
        var updated = await _konaklamaTipiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.KonaklamaTipiYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _konaklamaTipiService.DeleteAsync(id);
        return Ok();
    }
}

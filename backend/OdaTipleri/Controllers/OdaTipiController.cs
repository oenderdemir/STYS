using Microsoft.AspNetCore.Mvc;
using STYS.OdaTipleri.Dto;
using STYS.OdaTipleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaTipleri.Controllers;

public class OdaTipiController : UIController
{
    private readonly IOdaTipiService _odaTipiService;

    public OdaTipiController(IOdaTipiService odaTipiService)
    {
        _odaTipiService = odaTipiService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<List<OdaTipiDto>> GetAll()
    {
        var odaTipleri = await _odaTipiService.GetAllAsync();
        return odaTipleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaTipiDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _odaTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("by-tesis/{tesisId:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<List<OdaTipiDto>> GetByTesis(int tesisId)
    {
        if (tesisId <= 0)
        {
            return [];
        }

        var odaTipleri = await _odaTipiService.WhereAsync(x => x.TesisId == tesisId);
        return odaTipleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<ActionResult<OdaTipiDto>> GetById(int id)
    {
        var odaTipi = await _odaTipiService.GetByIdAsync(id);
        if (odaTipi is null)
        {
            return NotFound();
        }

        return Ok(odaTipi);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<ActionResult<OdaTipiDto>> Create([FromBody] OdaTipiDto dto)
    {
        var created = await _odaTipiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<ActionResult<OdaTipiDto>> Update(int id, [FromBody] OdaTipiDto dto)
    {
        dto.Id = id;
        var updated = await _odaTipiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaTipiService.DeleteAsync(id);
        return Ok();
    }
}

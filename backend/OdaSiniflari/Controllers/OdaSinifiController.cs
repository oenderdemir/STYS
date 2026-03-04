using Microsoft.AspNetCore.Mvc;
using STYS.OdaSiniflari.Dto;
using STYS.OdaSiniflari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaSiniflari.Controllers;

public class OdaSinifiController : UIController
{
    private readonly IOdaSinifiService _odaSinifiService;

    public OdaSinifiController(IOdaSinifiService odaSinifiService)
    {
        _odaSinifiService = odaSinifiService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<List<OdaSinifiDto>> GetAll()
    {
        var odaSiniflari = await _odaSinifiService.GetAllAsync();
        return odaSiniflari.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaSinifiDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: kod, ad, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _odaSinifiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<ActionResult<OdaSinifiDto>> GetById(int id)
    {
        var odaSinifi = await _odaSinifiService.GetByIdAsync(id);
        if (odaSinifi is null)
        {
            return NotFound();
        }

        return Ok(odaSinifi);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<ActionResult<OdaSinifiDto>> Create([FromBody] OdaSinifiDto dto)
    {
        var created = await _odaSinifiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<ActionResult<OdaSinifiDto>> Update(int id, [FromBody] OdaSinifiDto dto)
    {
        dto.Id = id;
        var updated = await _odaSinifiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaTipiYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaSinifiService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<STYS.OdaSiniflari.Entities.OdaSinifi>, IOrderedQueryable<STYS.OdaSiniflari.Entities.OdaSinifi>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "kod" => desc ? q => q.OrderByDescending(x => x.Kod).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Kod).ThenBy(x => x.Id),
            "ad" => desc ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

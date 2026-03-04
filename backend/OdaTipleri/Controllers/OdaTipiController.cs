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
    public async Task<ActionResult<PagedResult<OdaTipiDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: ad, tesisId, odaSinifiId, kapasite, paylasimliMi, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _odaTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
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

    private static Func<IQueryable<STYS.OdaTipleri.Entities.OdaTipi>, IOrderedQueryable<STYS.OdaTipleri.Entities.OdaTipi>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "ad" => desc ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "tesisid" => desc ? q => q.OrderByDescending(x => x.TesisId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.TesisId).ThenBy(x => x.Id),
            "odasinifiid" => desc ? q => q.OrderByDescending(x => x.OdaSinifiId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.OdaSinifiId).ThenBy(x => x.Id),
            "kapasite" => desc ? q => q.OrderByDescending(x => x.Kapasite).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Kapasite).ThenBy(x => x.Id),
            "paylasimlimi" => desc ? q => q.OrderByDescending(x => x.PaylasimliMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.PaylasimliMi).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

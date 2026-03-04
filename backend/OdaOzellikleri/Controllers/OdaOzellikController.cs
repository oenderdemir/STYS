using Microsoft.AspNetCore.Mvc;
using STYS.OdaOzellikleri.Dto;
using STYS.OdaOzellikleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaOzellikleri.Controllers;

public class OdaOzellikController : UIController
{
    private readonly IOdaOzellikService _odaOzellikService;

    public OdaOzellikController(IOdaOzellikService odaOzellikService)
    {
        _odaOzellikService = odaOzellikService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetAll()
    {
        var odaOzellikleri = await _odaOzellikService.GetAllAsync();
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("active")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetActive()
    {
        var odaOzellikleri = await _odaOzellikService.WhereAsync(x => x.AktifMi);
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("active-for-odatipi")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetActiveForOdaTipi()
    {
        var odaOzellikleri = await _odaOzellikService.WhereAsync(x => x.AktifMi);
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaOzellikDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: kod, ad, veriTipi, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _odaOzellikService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<ActionResult<OdaOzellikDto>> GetById(int id)
    {
        var odaOzellik = await _odaOzellikService.GetByIdAsync(id);
        if (odaOzellik is null)
        {
            return NotFound();
        }

        return Ok(odaOzellik);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<ActionResult<OdaOzellikDto>> Create([FromBody] OdaOzellikDto dto)
    {
        var created = await _odaOzellikService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<ActionResult<OdaOzellikDto>> Update(int id, [FromBody] OdaOzellikDto dto)
    {
        dto.Id = id;
        var updated = await _odaOzellikService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaOzellikService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<STYS.OdaOzellikleri.Entities.OdaOzellik>, IOrderedQueryable<STYS.OdaOzellikleri.Entities.OdaOzellik>>? BuildOrderBy(string? sortBy, string? sortDir)
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
            "veritipi" => desc ? q => q.OrderByDescending(x => x.VeriTipi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.VeriTipi).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

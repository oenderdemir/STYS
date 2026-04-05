using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Kamp.Controllers;

public class KampProgramiController : UIController
{
    private readonly IKampProgramiService _kampProgramiService;

    public KampProgramiController(IKampProgramiService kampProgramiService)
    {
        _kampProgramiService = kampProgramiService;
    }

    [HttpGet]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.View,
        StructurePermissions.KampProgramiTanimYonetimi.View)]
    public async Task<List<KampProgramiDto>> GetAll()
        => (await _kampProgramiService.GetAllAsync()).OrderBy(x => x.Ad).ToList();

    [HttpGet("paged")]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.View,
        StructurePermissions.KampProgramiTanimYonetimi.View)]
    public async Task<ActionResult<PagedResult<KampProgramiDto>>> GetPaged(
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
        var result = await _kampProgramiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.View,
        StructurePermissions.KampProgramiTanimYonetimi.View)]
    public async Task<ActionResult<KampProgramiDto>> GetById(int id)
    {
        var item = await _kampProgramiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.Manage,
        StructurePermissions.KampProgramiTanimYonetimi.Manage)]
    public async Task<ActionResult<KampProgramiDto>> Create([FromBody] KampProgramiDto dto)
    {
        var created = await _kampProgramiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.Manage,
        StructurePermissions.KampProgramiTanimYonetimi.Manage)]
    public async Task<ActionResult<KampProgramiDto>> Update(int id, [FromBody] KampProgramiDto dto)
    {
        dto.Id = id;
        var updated = await _kampProgramiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(
        StructurePermissions.KampProgramiYonetimi.Manage,
        StructurePermissions.KampProgramiTanimYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _kampProgramiService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<KampProgrami>, IOrderedQueryable<KampProgrami>>? BuildOrderBy(string? sortBy, string? sortDir)
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

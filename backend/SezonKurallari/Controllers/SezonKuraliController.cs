using Microsoft.AspNetCore.Mvc;
using STYS.SezonKurallari.Dto;
using STYS.SezonKurallari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.SezonKurallari.Controllers;

public class SezonKuraliController : UIController
{
    private readonly ISezonKuraliService _sezonKuraliService;

    public SezonKuraliController(ISezonKuraliService sezonKuraliService)
    {
        _sezonKuraliService = sezonKuraliService;
    }

    [HttpGet]
    [Permission(StructurePermissions.SezonYonetimi.View)]
    public async Task<List<SezonKuraliDto>> GetAll([FromQuery] int? tesisId)
    {
        var filtered = await _sezonKuraliService.WhereAsync(x =>
            !tesisId.HasValue || tesisId.Value <= 0 || x.TesisId == tesisId.Value);

        return filtered
            .OrderBy(x => x.TesisId)
            .ThenBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.Kod)
            .ThenBy(x => x.Id)
            .ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.SezonYonetimi.View)]
    public async Task<ActionResult<PagedResult<SezonKuraliDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? tesisId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: tesisId, kod, ad, baslangicTarihi, bitisTarihi, minimumGece, stopSaleMi, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _sezonKuraliService.GetPagedAsync(
            request,
            predicate: x =>
                (!tesisId.HasValue || tesisId.Value <= 0 || x.TesisId == tesisId.Value)
                && (string.IsNullOrWhiteSpace(normalizedQuery)
                    || x.Kod.Contains(normalizedQuery)
                    || x.Ad.Contains(normalizedQuery)),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.TesisId).ThenBy(x => x.BaslangicTarihi).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.SezonYonetimi.View)]
    public async Task<ActionResult<SezonKuraliDto>> GetById(int id)
    {
        var item = await _sezonKuraliService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.SezonYonetimi.Manage)]
    public async Task<ActionResult<SezonKuraliDto>> Create([FromBody] SezonKuraliDto dto)
    {
        var created = await _sezonKuraliService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.SezonYonetimi.Manage)]
    public async Task<ActionResult<SezonKuraliDto>> Update(int id, [FromBody] SezonKuraliDto dto)
    {
        dto.Id = id;
        var updated = await _sezonKuraliService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.SezonYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _sezonKuraliService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<STYS.SezonKurallari.Entities.SezonKurali>, IOrderedQueryable<STYS.SezonKurallari.Entities.SezonKurali>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "tesisid" => desc ? q => q.OrderByDescending(x => x.TesisId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.TesisId).ThenBy(x => x.Id),
            "kod" => desc ? q => q.OrderByDescending(x => x.Kod).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Kod).ThenBy(x => x.Id),
            "ad" => desc ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "baslangictarihi" => desc ? q => q.OrderByDescending(x => x.BaslangicTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BaslangicTarihi).ThenBy(x => x.Id),
            "bitistarihi" => desc ? q => q.OrderByDescending(x => x.BitisTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BitisTarihi).ThenBy(x => x.Id),
            "minimumgece" => desc ? q => q.OrderByDescending(x => x.MinimumGece).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.MinimumGece).ThenBy(x => x.Id),
            "stopsalemi" => desc ? q => q.OrderByDescending(x => x.StopSaleMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.StopSaleMi).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

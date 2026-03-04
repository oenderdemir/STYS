using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;
using STYS.Binalar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Binalar.Controllers;

public class BinaController : UIController
{
    private readonly IBinaService _binaService;

    public BinaController(IBinaService binaService)
    {
        _binaService = binaService;
    }

    [HttpGet]
    [Permission(StructurePermissions.BinaYonetimi.View)]
    public async Task<List<BinaDto>> GetAll()
    {
        var binalar = await _binaService.GetAllAsync();
        return binalar.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.BinaYonetimi.View)]
    public async Task<ActionResult<PagedResult<BinaDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery(Name = "tesisId")] int? tesisId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: ad, tesisId, katSayisi, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var normalizedTesisId = tesisId.HasValue && tesisId.Value > 0 ? tesisId.Value : (int?)null;

        Expression<Func<Bina, bool>>? predicate = null;
        if (!string.IsNullOrWhiteSpace(normalizedQuery) || normalizedTesisId.HasValue)
        {
            predicate = x =>
                (string.IsNullOrWhiteSpace(normalizedQuery) || x.Ad.Contains(normalizedQuery))
                && (!normalizedTesisId.HasValue || x.TesisId == normalizedTesisId.Value);
        }

        var result = await _binaService.GetPagedAsync(
            request,
            predicate: predicate,
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.BinaYonetimi.View)]
    public async Task<ActionResult<BinaDto>> GetById(int id)
    {
        var bina = await _binaService.GetByIdAsync(id);
        if (bina is null)
        {
            return NotFound();
        }

        return Ok(bina);
    }

    [HttpPost]
    [Permission(StructurePermissions.BinaYonetimi.Manage)]
    public async Task<ActionResult<BinaDto>> Create([FromBody] BinaDto dto)
    {
        var created = await _binaService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.BinaYonetimi.Manage)]
    public async Task<ActionResult<BinaDto>> Update(int id, [FromBody] BinaDto dto)
    {
        dto.Id = id;
        var updated = await _binaService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.BinaYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _binaService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<Bina>, IOrderedQueryable<Bina>>? BuildOrderBy(string? sortBy, string? sortDir)
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
            "katsayisi" => desc ? q => q.OrderByDescending(x => x.KatSayisi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KatSayisi).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using STYS.Odalar.Dto;
using STYS.Odalar.Entities;
using STYS.Odalar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Odalar.Controllers;

public class OdaController : UIController
{
    private readonly IOdaService _odaService;

    public OdaController(IOdaService odaService)
    {
        _odaService = odaService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<List<OdaDto>> GetAll()
    {
        var odalar = await _odaService.GetAllAsync();
        return odalar.OrderBy(x => x.OdaNo).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery(Name = "tesisId")] int? tesisId,
        [FromQuery(Name = "binaId")] int? binaId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: odaNo, binaId, tesisOdaTipiId, katNo, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var normalizedTesisId = tesisId.HasValue && tesisId.Value > 0 ? tesisId.Value : (int?)null;
        var normalizedBinaId = binaId.HasValue && binaId.Value > 0 ? binaId.Value : (int?)null;

        Expression<Func<Oda, bool>>? predicate = null;
        if (!string.IsNullOrWhiteSpace(normalizedQuery) || normalizedTesisId.HasValue || normalizedBinaId.HasValue)
        {
            predicate = x =>
                (string.IsNullOrWhiteSpace(normalizedQuery) || x.OdaNo.Contains(normalizedQuery))
                && (!normalizedBinaId.HasValue || x.BinaId == normalizedBinaId.Value)
                && (!normalizedTesisId.HasValue || (x.Bina != null && x.Bina.TesisId == normalizedTesisId.Value));
        }

        var result = await _odaService.GetPagedAsync(
            request,
            predicate: predicate,
            orderBy: orderBy ?? (q => q.OrderBy(x => x.OdaNo).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("by-bina/{binaId:int}")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<List<OdaDto>> GetByBina(int binaId)
    {
        if (binaId <= 0)
        {
            return [];
        }

        var odalar = await _odaService.WhereAsync(x => x.BinaId == binaId);
        return odalar.OrderBy(x => x.OdaNo).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<ActionResult<OdaDto>> GetById(int id)
    {
        var oda = await _odaService.GetByIdAsync(id);
        if (oda is null)
        {
            return NotFound();
        }

        return Ok(oda);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<ActionResult<OdaDto>> Create([FromBody] OdaDto dto)
    {
        var created = await _odaService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<ActionResult<OdaDto>> Update(int id, [FromBody] OdaDto dto)
    {
        dto.Id = id;
        var updated = await _odaService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<Oda>, IOrderedQueryable<Oda>>? BuildOrderBy(string? sortBy, string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();
        return normalized switch
        {
            "odano" => desc ? q => q.OrderByDescending(x => x.OdaNo).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.OdaNo).ThenBy(x => x.Id),
            "binaid" => desc ? q => q.OrderByDescending(x => x.BinaId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BinaId).ThenBy(x => x.Id),
            "tesisodatipiid" => desc ? q => q.OrderByDescending(x => x.TesisOdaTipiId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.TesisOdaTipiId).ThenBy(x => x.Id),
            "katno" => desc ? q => q.OrderByDescending(x => x.KatNo).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KatNo).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

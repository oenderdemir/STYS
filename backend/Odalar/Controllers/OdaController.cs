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
        [FromQuery(Name = "binaId")] int? binaId)
    {
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
            orderBy: q => q.OrderBy(x => x.OdaNo));
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
}

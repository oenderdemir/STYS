using Microsoft.AspNetCore.Mvc;
using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.IsletmeAlanlari.Controllers;

public class IsletmeAlaniController : UIController
{
    private readonly IIsletmeAlaniService _isletmeAlaniService;

    public IsletmeAlaniController(IIsletmeAlaniService isletmeAlaniService)
    {
        _isletmeAlaniService = isletmeAlaniService;
    }

    [HttpGet]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<List<IsletmeAlaniDto>> GetAll()
    {
        var alanlar = await _isletmeAlaniService.GetAllAsync();
        return alanlar.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<ActionResult<PagedResult<IsletmeAlaniDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildIsletmeAlaniOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: ad, sinifAd, ozelAd, binaId, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _isletmeAlaniService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x =>
                    (x.OzelAd != null && x.OzelAd.Contains(normalizedQuery))
                    || (x.IsletmeAlaniSinifi != null && x.IsletmeAlaniSinifi.Ad.Contains(normalizedQuery)),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.OzelAd ?? (x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty)).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("siniflar")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<List<IsletmeAlaniSinifiDto>> GetSiniflar([FromQuery] bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var siniflar = await _isletmeAlaniService.GetSiniflarAsync(onlyActive, cancellationToken);
        return siniflar.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("siniflar/paged")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<ActionResult<PagedResult<IsletmeAlaniSinifiDto>>> GetSiniflarPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var orderBy = BuildIsletmeAlaniSinifiOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: kod, ad, aktifMi, id, createdAt.");
        }

        var result = await _isletmeAlaniService.GetSiniflarPagedAsync(request, query, orderBy, cancellationToken);
        return Ok(result);
    }

    [HttpGet("siniflar/{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<ActionResult<IsletmeAlaniSinifiDto>> GetSinifById(int id, CancellationToken cancellationToken = default)
    {
        var sinif = await _isletmeAlaniService.GetSinifByIdAsync(id, cancellationToken);
        if (sinif is null)
        {
            return NotFound();
        }

        return Ok(sinif);
    }

    [HttpPost("siniflar")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<ActionResult<IsletmeAlaniSinifiDto>> CreateSinif([FromBody] IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default)
    {
        var created = await _isletmeAlaniService.AddSinifAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetSinifById), new { id = created.Id }, created);
    }

    [HttpPut("siniflar/{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<ActionResult<IsletmeAlaniSinifiDto>> UpdateSinif(int id, [FromBody] IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default)
    {
        dto.Id = id;
        var updated = await _isletmeAlaniService.UpdateSinifAsync(dto, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("siniflar/{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<IActionResult> DeleteSinif(int id, CancellationToken cancellationToken = default)
    {
        await _isletmeAlaniService.DeleteSinifAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("by-bina/{binaId:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<List<IsletmeAlaniDto>> GetByBina(int binaId)
    {
        if (binaId <= 0)
        {
            return [];
        }

        var alanlar = await _isletmeAlaniService.WhereAsync(x => x.BinaId == binaId);
        return alanlar.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.View)]
    public async Task<ActionResult<IsletmeAlaniDto>> GetById(int id)
    {
        var alan = await _isletmeAlaniService.GetByIdAsync(id);
        if (alan is null)
        {
            return NotFound();
        }

        return Ok(alan);
    }

    private static Func<IQueryable<Entities.IsletmeAlani>, IOrderedQueryable<Entities.IsletmeAlani>>? BuildIsletmeAlaniOrderBy(
        string? sortBy,
        string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();

        return normalized switch
        {
            "ad" => desc
                ? q => q.OrderByDescending(x => x.OzelAd ?? (x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty)).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.OzelAd ?? (x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty)).ThenBy(x => x.Id),
            "sinifad" => desc
                ? q => q.OrderByDescending(x => x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty).ThenBy(x => x.Id),
            "ozelad" => desc
                ? q => q.OrderByDescending(x => x.OzelAd ?? string.Empty).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.OzelAd ?? string.Empty).ThenBy(x => x.Id),
            "binaid" => desc
                ? q => q.OrderByDescending(x => x.BinaId).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.BinaId).ThenBy(x => x.Id),
            "aktifmi" => desc
                ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc
                ? q => q.OrderByDescending(x => x.Id)
                : q => q.OrderBy(x => x.Id),
            "createdat" => desc
                ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }

    private static Func<IQueryable<Entities.IsletmeAlaniSinifi>, IOrderedQueryable<Entities.IsletmeAlaniSinifi>>? BuildIsletmeAlaniSinifiOrderBy(
        string? sortBy,
        string? sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return null;
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var normalized = sortBy.Trim().ToLowerInvariant();

        return normalized switch
        {
            "kod" => desc
                ? q => q.OrderByDescending(x => x.Kod).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.Kod).ThenBy(x => x.Id),
            "ad" => desc
                ? q => q.OrderByDescending(x => x.Ad).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id),
            "aktifmi" => desc
                ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc
                ? q => q.OrderByDescending(x => x.Id)
                : q => q.OrderBy(x => x.Id),
            "createdat" => desc
                ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

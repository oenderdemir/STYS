using Microsoft.AspNetCore.Mvc;
using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.MisafirTipleri.Controllers;

public class MisafirTipiController : UIController
{
    private readonly IMisafirTipiService _misafirTipiService;

    public MisafirTipiController(IMisafirTipiService misafirTipiService)
    {
        _misafirTipiService = misafirTipiService;
    }

    [HttpGet]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.View,
        StructurePermissions.MisafirTipiTanimYonetimi.View,
        StructurePermissions.MisafirTipiTesisAtamaYonetimi.View)]
    public async Task<List<MisafirTipiDto>> GetAll([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _misafirTipiService.GetAktifMisafirTipleriByTesisAsync(tesisId.Value, cancellationToken)
            : (await _misafirTipiService.GetAllAsync()).ToList();

        return items.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("yonetim-baglam")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.View,
        StructurePermissions.MisafirTipiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<MisafirTipiYonetimBaglamDto>> GetYonetimBaglam(CancellationToken cancellationToken)
    {
        var result = await _misafirTipiService.GetYonetimBaglamAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("tesis/{tesisId:int}/atamalar")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.View,
        StructurePermissions.MisafirTipiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<List<MisafirTipiTesisAtamaDto>>> GetTesisAtamalari([FromRoute] int tesisId, CancellationToken cancellationToken)
    {
        var result = await _misafirTipiService.GetTesisAtamalariAsync(tesisId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("tesis/{tesisId:int}/atamalar")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.Manage,
        StructurePermissions.MisafirTipiTesisAtamaYonetimi.Manage)]
    public async Task<ActionResult<List<MisafirTipiTesisAtamaDto>>> KaydetTesisAtamalari(
        [FromRoute] int tesisId,
        [FromBody] MisafirTipiTesisAtamaKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _misafirTipiService.KaydetTesisAtamalariAsync(tesisId, request.MisafirTipiIds, cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.View,
        StructurePermissions.MisafirTipiTanimYonetimi.View)]
    public async Task<ActionResult<PagedResult<MisafirTipiDto>>> GetPaged(
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
        var result = await _misafirTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.View,
        StructurePermissions.MisafirTipiTanimYonetimi.View)]
    public async Task<ActionResult<MisafirTipiDto>> GetById(int id)
    {
        var item = await _misafirTipiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.Manage,
        StructurePermissions.MisafirTipiTanimYonetimi.Manage)]
    public async Task<ActionResult<MisafirTipiDto>> Create([FromBody] MisafirTipiDto dto)
    {
        var created = await _misafirTipiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.Manage,
        StructurePermissions.MisafirTipiTanimYonetimi.Manage)]
    public async Task<ActionResult<MisafirTipiDto>> Update(int id, [FromBody] MisafirTipiDto dto)
    {
        dto.Id = id;
        var updated = await _misafirTipiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(
        StructurePermissions.MisafirTipiYonetimi.Manage,
        StructurePermissions.MisafirTipiTanimYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _misafirTipiService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<MisafirTipleri.Entities.MisafirTipi>, IOrderedQueryable<MisafirTipleri.Entities.MisafirTipi>>? BuildOrderBy(string? sortBy, string? sortDir)
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

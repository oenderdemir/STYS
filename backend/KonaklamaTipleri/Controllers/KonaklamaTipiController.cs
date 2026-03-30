using Microsoft.AspNetCore.Mvc;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.KonaklamaTipleri.Controllers;

public class KonaklamaTipiController : UIController
{
    private readonly IKonaklamaTipiService _konaklamaTipiService;

    public KonaklamaTipiController(IKonaklamaTipiService konaklamaTipiService)
    {
        _konaklamaTipiService = konaklamaTipiService;
    }

    [HttpGet]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTanimYonetimi.View,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.View)]
    public async Task<List<KonaklamaTipiDto>> GetAll([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _konaklamaTipiService.GetAktifKonaklamaTipleriByTesisAsync(tesisId.Value, cancellationToken)
            : (await _konaklamaTipiService.GetAllAsync()).ToList();

        return items.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("yonetim-baglam")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<KonaklamaTipiYonetimBaglamDto>> GetYonetimBaglam(CancellationToken cancellationToken)
    {
        var result = await _konaklamaTipiService.GetYonetimBaglamAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("tesis/{tesisId:int}/atamalar")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<List<KonaklamaTipiTesisAtamaDto>>> GetTesisAtamalari([FromRoute] int tesisId, CancellationToken cancellationToken)
    {
        var result = await _konaklamaTipiService.GetTesisAtamalariAsync(tesisId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("tesis/{tesisId:int}/atamalar")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.Manage,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.Manage)]
    public async Task<ActionResult<List<KonaklamaTipiTesisAtamaDto>>> KaydetTesisAtamalari(
        [FromRoute] int tesisId,
        [FromBody] KonaklamaTipiTesisAtamaKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _konaklamaTipiService.KaydetTesisAtamalariAsync(tesisId, request.KonaklamaTipiIds, cancellationToken);
        return Ok(result);
    }

    [HttpGet("tesis/{tesisId:int}/atamalar/{konaklamaTipiId:int}/icerik-override")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.View)]
    public async Task<ActionResult<List<KonaklamaTipiTesisIcerikOverrideDto>>> GetTesisIcerikOverride(
        [FromRoute] int tesisId,
        [FromRoute] int konaklamaTipiId,
        CancellationToken cancellationToken)
    {
        var result = await _konaklamaTipiService.GetTesisIcerikOverrideAsync(tesisId, konaklamaTipiId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("tesis/{tesisId:int}/atamalar/{konaklamaTipiId:int}/icerik-override")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.Manage,
        StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.Manage)]
    public async Task<ActionResult<List<KonaklamaTipiTesisIcerikOverrideDto>>> KaydetTesisIcerikOverride(
        [FromRoute] int tesisId,
        [FromRoute] int konaklamaTipiId,
        [FromBody] KonaklamaTipiTesisIcerikOverrideKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _konaklamaTipiService.KaydetTesisIcerikOverrideAsync(tesisId, konaklamaTipiId, request.IcerikKalemleri, cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTanimYonetimi.View)]
    public async Task<ActionResult<PagedResult<KonaklamaTipiDto>>> GetPaged(
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
        var result = await _konaklamaTipiService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.View,
        StructurePermissions.KonaklamaTipiTanimYonetimi.View)]
    public async Task<ActionResult<KonaklamaTipiDto>> GetById(int id)
    {
        var item = await _konaklamaTipiService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.Manage,
        StructurePermissions.KonaklamaTipiTanimYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaTipiDto>> Create([FromBody] KonaklamaTipiDto dto)
    {
        var created = await _konaklamaTipiService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.Manage,
        StructurePermissions.KonaklamaTipiTanimYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaTipiDto>> Update(int id, [FromBody] KonaklamaTipiDto dto)
    {
        dto.Id = id;
        var updated = await _konaklamaTipiService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(
        StructurePermissions.KonaklamaTipiYonetimi.Manage,
        StructurePermissions.KonaklamaTipiTanimYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _konaklamaTipiService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<STYS.KonaklamaTipleri.Entities.KonaklamaTipi>, IOrderedQueryable<STYS.KonaklamaTipleri.Entities.KonaklamaTipi>>? BuildOrderBy(string? sortBy, string? sortDir)
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

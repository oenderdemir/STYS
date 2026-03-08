using Microsoft.AspNetCore.Mvc;
using STYS.OdaKullanimBloklari.Dto;
using STYS.OdaKullanimBloklari.Entities;
using STYS.OdaKullanimBloklari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaKullanimBloklari.Controllers;

public class OdaKullanimBlokController : UIController
{
    private readonly IOdaKullanimBlokService _odaKullanimBlokService;

    public OdaKullanimBlokController(IOdaKullanimBlokService odaKullanimBlokService)
    {
        _odaKullanimBlokService = odaKullanimBlokService;
    }

    [HttpGet("tesisler")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.View)]
    public async Task<ActionResult<List<OdaKullanimBlokTesisDto>>> GetTesisler(CancellationToken cancellationToken)
    {
        var tesisler = await _odaKullanimBlokService.GetErisilebilirTesislerAsync(cancellationToken);
        return Ok(tesisler);
    }

    [HttpGet("odalar")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.View)]
    public async Task<ActionResult<List<OdaKullanimBlokOdaSecenekDto>>> GetOdalar(
        [FromQuery] int tesisId,
        CancellationToken cancellationToken)
    {
        var odalar = await _odaKullanimBlokService.GetOdaSecenekleriAsync(tesisId, cancellationToken);
        return Ok(odalar);
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaKullanimBlokDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? tesisId,
        [FromQuery] int? odaId,
        [FromQuery] string? blokTipi,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "desc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: tesisId, odaId, blokTipi, baslangicTarihi, bitisTarihi, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var normalizedBlokTipi = blokTipi?.Trim();

        var result = await _odaKullanimBlokService.GetPagedAsync(
            request,
            predicate: x =>
                (!tesisId.HasValue || tesisId.Value <= 0 || x.TesisId == tesisId.Value)
                && (!odaId.HasValue || odaId.Value <= 0 || x.OdaId == odaId.Value)
                && (string.IsNullOrWhiteSpace(normalizedBlokTipi) || x.BlokTipi == normalizedBlokTipi)
                && (string.IsNullOrWhiteSpace(normalizedQuery)
                    || (x.Aciklama != null && x.Aciklama.Contains(normalizedQuery))),
            orderBy: orderBy ?? (q => q.OrderByDescending(x => x.BaslangicTarihi).ThenByDescending(x => x.Id)));

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.View)]
    public async Task<ActionResult<OdaKullanimBlokDto>> GetById(int id)
    {
        var item = await _odaKullanimBlokService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.Manage)]
    public async Task<ActionResult<OdaKullanimBlokDto>> Create([FromBody] OdaKullanimBlokDto dto)
    {
        var created = await _odaKullanimBlokService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.Manage)]
    public async Task<ActionResult<OdaKullanimBlokDto>> Update(int id, [FromBody] OdaKullanimBlokDto dto)
    {
        dto.Id = id;
        var updated = await _odaKullanimBlokService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaKullanimBlokYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaKullanimBlokService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<OdaKullanimBlok>, IOrderedQueryable<OdaKullanimBlok>>? BuildOrderBy(string? sortBy, string? sortDir)
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
            "odaid" => desc ? q => q.OrderByDescending(x => x.OdaId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.OdaId).ThenBy(x => x.Id),
            "bloktipi" => desc ? q => q.OrderByDescending(x => x.BlokTipi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BlokTipi).ThenBy(x => x.Id),
            "baslangictarihi" => desc ? q => q.OrderByDescending(x => x.BaslangicTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BaslangicTarihi).ThenBy(x => x.Id),
            "bitistarihi" => desc ? q => q.OrderByDescending(x => x.BitisTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BitisTarihi).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

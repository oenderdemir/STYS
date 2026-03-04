using Microsoft.AspNetCore.Mvc;
using STYS.Fiyatlandirma.Dto;
using STYS.Fiyatlandirma.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Fiyatlandirma.Controllers;

public class IndirimKuraliController : UIController
{
    private readonly IIndirimKuraliService _indirimKuraliService;

    public IndirimKuraliController(IIndirimKuraliService indirimKuraliService)
    {
        _indirimKuraliService = indirimKuraliService;
    }

    [HttpGet]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<List<IndirimKuraliDto>> GetAll()
    {
        var items = await _indirimKuraliService.GetAllAsync();
        return items.OrderByDescending(x => x.Oncelik).ThenBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<ActionResult<PagedResult<IndirimKuraliDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc")
    {
        var orderBy = BuildOrderBy(sortBy, sortDir);
        if (orderBy is null && !string.IsNullOrWhiteSpace(sortBy))
        {
            return BadRequest("Desteklenmeyen siralama kolonu. Desteklenen alanlar: kod, ad, indirimTipi, deger, kapsamTipi, tesisId, baslangicTarihi, bitisTarihi, oncelik, aktifMi, id, createdAt.");
        }

        var normalizedQuery = query?.Trim();
        var result = await _indirimKuraliService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: orderBy ?? (q => q.OrderByDescending(x => x.Oncelik).ThenBy(x => x.Ad).ThenBy(x => x.Id)));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.View)]
    public async Task<ActionResult<IndirimKuraliDto>> GetById(int id)
    {
        var item = await _indirimKuraliService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<ActionResult<IndirimKuraliDto>> Create([FromBody] IndirimKuraliDto dto)
    {
        var created = await _indirimKuraliService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<ActionResult<IndirimKuraliDto>> Update(int id, [FromBody] IndirimKuraliDto dto)
    {
        dto.Id = id;
        var updated = await _indirimKuraliService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.IndirimKuraliYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _indirimKuraliService.DeleteAsync(id);
        return Ok();
    }

    private static Func<IQueryable<STYS.Fiyatlandirma.Entities.IndirimKurali>, IOrderedQueryable<STYS.Fiyatlandirma.Entities.IndirimKurali>>? BuildOrderBy(string? sortBy, string? sortDir)
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
            "indirimtipi" => desc ? q => q.OrderByDescending(x => x.IndirimTipi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.IndirimTipi).ThenBy(x => x.Id),
            "deger" => desc ? q => q.OrderByDescending(x => x.Deger).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Deger).ThenBy(x => x.Id),
            "kapsamtipi" => desc ? q => q.OrderByDescending(x => x.KapsamTipi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.KapsamTipi).ThenBy(x => x.Id),
            "tesisid" => desc ? q => q.OrderByDescending(x => x.TesisId).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.TesisId).ThenBy(x => x.Id),
            "baslangictarihi" => desc ? q => q.OrderByDescending(x => x.BaslangicTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BaslangicTarihi).ThenBy(x => x.Id),
            "bitistarihi" => desc ? q => q.OrderByDescending(x => x.BitisTarihi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.BitisTarihi).ThenBy(x => x.Id),
            "oncelik" => desc ? q => q.OrderByDescending(x => x.Oncelik).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.Oncelik).ThenBy(x => x.Id),
            "aktifmi" => desc ? q => q.OrderByDescending(x => x.AktifMi).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.AktifMi).ThenBy(x => x.Id),
            "id" => desc ? q => q.OrderByDescending(x => x.Id) : q => q.OrderBy(x => x.Id),
            "createdat" => desc ? q => q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id) : q => q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => null
        };
    }
}

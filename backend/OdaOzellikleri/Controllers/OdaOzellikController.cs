using Microsoft.AspNetCore.Mvc;
using STYS.OdaOzellikleri.Dto;
using STYS.OdaOzellikleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaOzellikleri.Controllers;

public class OdaOzellikController : UIController
{
    private readonly IOdaOzellikService _odaOzellikService;

    public OdaOzellikController(IOdaOzellikService odaOzellikService)
    {
        _odaOzellikService = odaOzellikService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetAll()
    {
        var odaOzellikleri = await _odaOzellikService.GetAllAsync();
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("active")]
    [Permission(StructurePermissions.OdaYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetActive()
    {
        var odaOzellikleri = await _odaOzellikService.WhereAsync(x => x.AktifMi);
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("active-for-odatipi")]
    [Permission(StructurePermissions.OdaTipiYonetimi.View)]
    public async Task<List<OdaOzellikDto>> GetActiveForOdaTipi()
    {
        var odaOzellikleri = await _odaOzellikService.WhereAsync(x => x.AktifMi);
        return odaOzellikleri.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaOzellikDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _odaOzellikService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.View)]
    public async Task<ActionResult<OdaOzellikDto>> GetById(int id)
    {
        var odaOzellik = await _odaOzellikService.GetByIdAsync(id);
        if (odaOzellik is null)
        {
            return NotFound();
        }

        return Ok(odaOzellik);
    }

    [HttpPost]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<ActionResult<OdaOzellikDto>> Create([FromBody] OdaOzellikDto dto)
    {
        var created = await _odaOzellikService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<ActionResult<OdaOzellikDto>> Update(int id, [FromBody] OdaOzellikDto dto)
    {
        dto.Id = id;
        var updated = await _odaOzellikService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.OdaOzellikYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _odaOzellikService.DeleteAsync(id);
        return Ok();
    }
}

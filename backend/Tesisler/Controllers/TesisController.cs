using Microsoft.AspNetCore.Mvc;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Tesisler.Controllers;

public class TesisController : UIController
{
    private readonly ITesisService _tesisService;

    public TesisController(ITesisService tesisService)
    {
        _tesisService = tesisService;
    }

    [HttpGet]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<List<TesisDto>> GetAll()
    {
        var tesisler = await _tesisService.GetAllAsync();
        return tesisler.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<ActionResult<PagedResult<TesisDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _tesisService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery)
                ? null
                : x => x.Ad.Contains(normalizedQuery)
                    || x.Telefon.Contains(normalizedQuery)
                    || x.Adres.Contains(normalizedQuery)
                    || (x.Eposta != null && x.Eposta.Contains(normalizedQuery)),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
    }

    [HttpGet("by-il/{ilId:int}")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<List<TesisDto>> GetByIl(int ilId)
    {
        if (ilId <= 0)
        {
            return [];
        }

        var tesisler = await _tesisService.WhereAsync(x => x.IlId == ilId);
        return tesisler.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.View)]
    public async Task<ActionResult<TesisDto>> GetById(int id)
    {
        var tesis = await _tesisService.GetByIdAsync(id);
        if (tesis is null)
        {
            return NotFound();
        }

        return Ok(tesis);
    }

    [HttpPost]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<ActionResult<TesisDto>> Create([FromBody] TesisDto dto)
    {
        var created = await _tesisService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<ActionResult<TesisDto>> Update(int id, [FromBody] TesisDto dto)
    {
        dto.Id = id;
        var updated = await _tesisService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TesisYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _tesisService.DeleteAsync(id);
        return Ok();
    }
}

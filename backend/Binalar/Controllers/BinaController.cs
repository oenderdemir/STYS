using Microsoft.AspNetCore.Mvc;
using STYS.Binalar.Dto;
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
    public async Task<ActionResult<PagedResult<BinaDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _binaService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery) ? null : x => x.Ad.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
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
}

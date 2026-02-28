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
    public async Task<ActionResult<PagedResult<IsletmeAlaniDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query)
    {
        var normalizedQuery = query?.Trim();
        var result = await _isletmeAlaniService.GetPagedAsync(
            request,
            predicate: string.IsNullOrWhiteSpace(normalizedQuery) ? null : x => x.Ad.Contains(normalizedQuery),
            orderBy: q => q.OrderBy(x => x.Ad));
        return Ok(result);
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

    [HttpPost]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<ActionResult<IsletmeAlaniDto>> Create([FromBody] IsletmeAlaniDto dto)
    {
        var created = await _isletmeAlaniService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<ActionResult<IsletmeAlaniDto>> Update(int id, [FromBody] IsletmeAlaniDto dto)
    {
        dto.Id = id;
        var updated = await _isletmeAlaniService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.IsletmeAlaniYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _isletmeAlaniService.DeleteAsync(id);
        return Ok();
    }
}

using Microsoft.AspNetCore.Mvc;
using STYS.Iller.Dto;
using STYS.Iller.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Iller.Controllers;

public class IlController : UIController
{
    private readonly IIlService _ilService;

    public IlController(IIlService ilService)
    {
        _ilService = ilService;
    }

    [HttpGet]
    [Permission(StructurePermissions.IlYonetimi.View)]
    public async Task<List<IlDto>> GetAll()
    {
        var iller = await _ilService.GetAllAsync();
        return iller.OrderBy(x => x.Ad).ToList();
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.IlYonetimi.View)]
    public async Task<ActionResult<IlDto>> GetById(int id)
    {
        var il = await _ilService.GetByIdAsync(id);
        if (il is null)
        {
            return NotFound();
        }

        return Ok(il);
    }

    [HttpPost]
    [Permission(StructurePermissions.IlYonetimi.Manage)]
    public async Task<ActionResult<IlDto>> Create([FromBody] IlDto dto)
    {
        var created = await _ilService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.IlYonetimi.Manage)]
    public async Task<ActionResult<IlDto>> Update(int id, [FromBody] IlDto dto)
    {
        dto.Id = id;
        var updated = await _ilService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.IlYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        await _ilService.DeleteAsync(id);
        return Ok();
    }
}
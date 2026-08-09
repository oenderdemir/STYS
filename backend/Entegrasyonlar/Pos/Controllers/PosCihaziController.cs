using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pos.Controllers;

[Route("ui/pos")]
public sealed class PosCihaziController : UIController
{
    private readonly IPosCihaziService _service;
    private readonly IMapper _mapper;

    public PosCihaziController(IPosCihaziService service, IMapper mapper) { _service = service; _mapper = mapper; }

    [HttpGet("cihazlar")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<IEnumerable<PosCihaziDto>>> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.View)]
    public async Task<ActionResult<PosCihaziDto>> GetById(int id) => Ok(await _service.GetByIdAsync(id));

    [HttpPost("cihazlar")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Create([FromBody] PosCihaziKaydetRequest req)
    {
        var dto = _mapper.Map<PosCihaziDto>(req);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Update(int id, [FromBody] PosCihaziKaydetRequest req)
    {
        var dto = _mapper.Map<PosCihaziDto>(req);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("cihazlar/{id:int}")]
    [Permission(StructurePermissions.PosYonetimi.Manage)]
    public async Task<ActionResult> Delete(int id) { await _service.DeleteAsync(id); return Ok(); }
}

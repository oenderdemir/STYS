using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Kdv.Dtos;
using STYS.Muhasebe.Kdv.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.Kdv.Controllers;

[Route("ui/muhasebe/kdv-istisna-tanimlari")]
public class KdvIstisnaTanimController : UIController
{
    private readonly IKdvIstisnaTanimService _service;
    private readonly IMapper _mapper;

    public KdvIstisnaTanimController(
        IKdvIstisnaTanimService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<ActionResult<IEnumerable<KdvIstisnaTanimDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<ActionResult<KdvIstisnaTanimDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
    public async Task<ActionResult<KdvIstisnaTanimDto>> Create(
        [FromBody] CreateKdvIstisnaTanimRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KdvIstisnaTanimDto>(request);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
    public async Task<ActionResult<KdvIstisnaTanimDto>> Update(
        int id,
        [FromBody] UpdateKdvIstisnaTanimRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KdvIstisnaTanimDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

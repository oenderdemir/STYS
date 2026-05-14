using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.MuhasebeFisleri.Controllers;

[Route("ui/muhasebe/fisler")]
public class MuhasebeFisController : UIController
{
    private readonly IMuhasebeFisService _service;
    private readonly IMapper _mapper;

    public MuhasebeFisController(
        IMuhasebeFisService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeFisDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<ActionResult<MuhasebeFisDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdWithSatirlarAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("by-kaynak")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeFisDto>>> GetByKaynak(
        [FromQuery] string kaynakModul,
        [FromQuery] int kaynakId,
        CancellationToken cancellationToken)
    {
        var items = await _service.GetByKaynakAsync(kaynakModul, kaynakId, cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeFisDto>> Create(
        [FromBody] CreateMuhasebeFisRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeFisDto>(request);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeFisYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeFisDto>> Update(
        int id,
        [FromBody] UpdateMuhasebeFisRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeFisDto>(request);
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

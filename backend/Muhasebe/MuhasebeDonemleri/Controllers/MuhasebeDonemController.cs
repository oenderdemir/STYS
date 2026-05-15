using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeDonemleri.Dtos;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.MuhasebeDonemleri.Controllers;

[Route("ui/muhasebe/donemler")]
public class MuhasebeDonemController : UIController
{
    private readonly IMuhasebeDonemService _service;
    private readonly IMapper _mapper;

    public MuhasebeDonemController(
        IMuhasebeDonemService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeDonemDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.View)]
    public async Task<ActionResult<MuhasebeDonemDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("aktif")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.View)]
    public async Task<ActionResult<MuhasebeDonemDto>> GetAktif(
        [FromQuery] int tesisId,
        [FromQuery] DateTime tarih,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetAktifDonemAsync(tesisId, tarih, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeDonemDto>> Create(
        [FromBody] CreateMuhasebeDonemRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeDonemDto>(request);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeDonemDto>> Update(
        int id,
        [FromBody] UpdateMuhasebeDonemRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeDonemDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("{id:int}/kapat")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.ClosePeriod)]
    public async Task<IActionResult> Kapat(int id, CancellationToken cancellationToken)
    {
        await _service.DonemKapatAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/ac")]
    [Permission(StructurePermissions.MuhasebeDonemYonetimi.ClosePeriod)]
    public async Task<IActionResult> Ac(int id, CancellationToken cancellationToken)
    {
        await _service.DonemAcAsync(id, cancellationToken);
        return Ok();
    }
}

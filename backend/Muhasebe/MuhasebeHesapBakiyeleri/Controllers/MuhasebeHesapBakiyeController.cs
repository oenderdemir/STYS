using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Dtos;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.MuhasebeHesapBakiyeleri.Controllers;

[Route("ui/muhasebe/hesap-bakiyeleri")]
public class MuhasebeHesapBakiyeController : UIController
{
    private readonly IMuhasebeHesapBakiyeService _service;
    private readonly IMapper _mapper;

    public MuhasebeHesapBakiyeController(
        IMuhasebeHesapBakiyeService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeHesapBakiyeDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.View)]
    public async Task<ActionResult<MuhasebeHesapBakiyeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapBakiyeDto>> Create(
        [FromBody] CreateMuhasebeHesapBakiyeRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<MuhasebeHesapBakiyeDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapBakiyeDto>> Update(
        int id,
        [FromBody] UpdateMuhasebeHesapBakiyeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeHesapBakiyeDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("filter")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeHesapBakiyeDto>>> Filter(
        [FromBody] MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.GetFilteredAsync(filter, cancellationToken));

    [HttpPost("filter/count")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.View)]
    public async Task<ActionResult<int>> Count(
        [FromBody] MuhasebeHesapBakiyeFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _service.CountFilteredAsync(filter, cancellationToken));

    [HttpGet("by-donem")]
    [Permission(StructurePermissions.MuhasebeHesapBakiyeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeHesapBakiyeDto>>> GetByDonem(
        [FromQuery] int tesisId,
        [FromQuery] int maliYil,
        [FromQuery] int donem,
        CancellationToken cancellationToken)
        => Ok(await _service.GetByTesisYilDonemAsync(tesisId, maliYil, donem, cancellationToken));
}

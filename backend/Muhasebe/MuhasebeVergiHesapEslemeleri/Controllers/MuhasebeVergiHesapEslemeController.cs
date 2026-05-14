using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Dtos;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Controllers;

[Route("api/muhasebe/vergi-hesap-esleme")]
[ApiController]
public class MuhasebeVergiHesapEslemeController : UIController
{
    private readonly IMuhasebeVergiHesapEslemeService _service;
    private readonly IMapper _mapper;

    public MuhasebeVergiHesapEslemeController(
        IMuhasebeVergiHesapEslemeService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet("aktif")]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<MuhasebeVergiHesapEslemeDto>> GetAktif(
        [FromQuery] string vergiTipi,
        [FromQuery] decimal oran,
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetAktifEslemeAsync(vergiTipi, oran, tesisId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<MuhasebeVergiHesapEslemeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<MuhasebeVergiHesapEslemeDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeVergiHesapEslemeDto>> Create(
        [FromBody] CreateMuhasebeVergiHesapEslemeRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<MuhasebeVergiHesapEslemeDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeVergiHesapEslemeDto>> Update(
        int id,
        [FromBody] UpdateMuhasebeVergiHesapEslemeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeVergiHesapEslemeDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeVergiHesapEslemeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

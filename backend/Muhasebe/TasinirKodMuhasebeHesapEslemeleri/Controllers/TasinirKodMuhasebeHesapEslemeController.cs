using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Dtos;
using STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.TasinirKodMuhasebeHesapEslemeleri.Controllers;

[Route("ui/muhasebe/tasinir-kod-muhasebe-hesap-esleme")]
public class TasinirKodMuhasebeHesapEslemeController : UIController
{
    private readonly ITasinirKodMuhasebeHesapEslemeService _service;
    private readonly IMapper _mapper;

    public TasinirKodMuhasebeHesapEslemeController(
        ITasinirKodMuhasebeHesapEslemeService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet("by-tasinir-kod/{tasinirKodId:int}")]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.View)]
    public async Task<ActionResult<List<TasinirKodMuhasebeHesapEslemeDto>>> GetByTasinirKodId(int tasinirKodId, CancellationToken cancellationToken)
        => Ok(await _service.GetByTasinirKodIdAsync(tasinirKodId, cancellationToken));

    [HttpGet("varsayilan")]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.View)]
    public async Task<ActionResult<TasinirKodMuhasebeHesapEslemeDto>> GetVarsayilan([FromQuery] int tasinirKodId, [FromQuery] string malzemeTipi, [FromQuery] string hareketTipi, CancellationToken cancellationToken)
    {
        var item = await _service.GetVarsayilanAsync(tasinirKodId, malzemeTipi, hareketTipi, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.View)]
    public async Task<ActionResult<TasinirKodMuhasebeHesapEslemeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<TasinirKodMuhasebeHesapEslemeDto>> Create([FromBody] CreateTasinirKodMuhasebeHesapEslemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<TasinirKodMuhasebeHesapEslemeDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<TasinirKodMuhasebeHesapEslemeDto>> Update(int id, [FromBody] UpdateTasinirKodMuhasebeHesapEslemeRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TasinirKodMuhasebeHesapEslemeDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TasinirKodMuhasebeHesapEslemeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}
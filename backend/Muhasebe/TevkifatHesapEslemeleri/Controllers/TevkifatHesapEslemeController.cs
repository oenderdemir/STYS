using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Dtos;
using STYS.Muhasebe.TevkifatHesapEslemeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Controllers;

[Route("ui/muhasebe/tevkifat-hesap-eslemeleri")]
public class TevkifatHesapEslemeController : UIController
{
    private readonly ITevkifatHesapEslemeService _service;
    private readonly IMapper _mapper;

    public TevkifatHesapEslemeController(
        ITevkifatHesapEslemeService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<TevkifatHesapEslemeDto>>> GetAll(
        [FromQuery] int? tesisId,
        [FromQuery] string? islemYonu,
        [FromQuery] bool? aktifMi,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(tesisId, islemYonu, aktifMi, cancellationToken));

    [HttpGet("paged")]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.View)]
    public async Task<ActionResult<PagedResult<TevkifatHesapEslemeDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] int? tesisId,
        [FromQuery] string? islemYonu,
        [FromQuery] bool? aktifMi,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, tesisId, islemYonu, aktifMi, cancellationToken));

    [HttpGet("aktif")]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.View)]
    public async Task<ActionResult<TevkifatHesapEslemeDto>> GetAktif(
        [FromQuery] int? tesisId,
        [FromQuery] string islemYonu,
        [FromQuery] int tevkifatPay,
        [FromQuery] int tevkifatPayda,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetAktifEslemeAsync(tesisId, islemYonu, tevkifatPay, tevkifatPayda, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.View)]
    public async Task<ActionResult<TevkifatHesapEslemeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<TevkifatHesapEslemeDto>> Create(
        [FromBody] CreateTevkifatHesapEslemeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TevkifatHesapEslemeDto>(request);
        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<TevkifatHesapEslemeDto>> Update(
        int id,
        [FromBody] UpdateTevkifatHesapEslemeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TevkifatHesapEslemeDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeTevkifatHesapEslemeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

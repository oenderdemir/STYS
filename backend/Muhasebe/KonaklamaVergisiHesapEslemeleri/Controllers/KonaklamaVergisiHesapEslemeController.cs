using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Dtos;
using STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.KonaklamaVergisiHesapEslemeleri.Controllers;

[Route("ui/muhasebe/konaklama-vergisi-hesap-eslemeleri")]
public class KonaklamaVergisiHesapEslemeController : UIController
{
    private readonly IKonaklamaVergisiHesapEslemeService _service;
    private readonly IMapper _mapper;

    public KonaklamaVergisiHesapEslemeController(
        IKonaklamaVergisiHesapEslemeService service,
        IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<IEnumerable<KonaklamaVergisiHesapEslemeDto>>> GetAll(
        [FromQuery] int? tesisId,
        [FromQuery] bool? aktifMi,
        CancellationToken cancellationToken)
        => Ok(await _service.GetAllAsync(tesisId, aktifMi, cancellationToken));

    [HttpGet("paged")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<PagedResult<KonaklamaVergisiHesapEslemeDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] int? tesisId,
        [FromQuery] bool? aktifMi,
        CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, tesisId, aktifMi, cancellationToken));

    [HttpGet("aktif")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<KonaklamaVergisiHesapEslemeDto>> GetAktif(
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken)
    {
        var item = await _service.GetAktifEslemeAsync(tesisId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("hesap-secenekleri")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<List<KonaklamaVergisiHesapSecenekDto>>> GetHesapSecenekleri(
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetHesapSecenekleriAsync(tesisId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.View)]
    public async Task<ActionResult<KonaklamaVergisiHesapEslemeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaVergisiHesapEslemeDto>> Create(
        [FromBody] CreateKonaklamaVergisiHesapEslemeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KonaklamaVergisiHesapEslemeDto>(request);
        return Ok(await _service.AddAsync(dto, cancellationToken));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.Manage)]
    public async Task<ActionResult<KonaklamaVergisiHesapEslemeDto>> Update(
        int id,
        [FromBody] UpdateKonaklamaVergisiHesapEslemeRequest request,
        CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KonaklamaVergisiHesapEslemeDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeKonaklamaVergisiHesapEslemeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }
}

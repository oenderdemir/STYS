using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Controllers;

[Route("ui/muhasebe/tahsilat-odeme-belgeleri")]
public class TahsilatOdemeBelgeleriController : UIController
{
    private readonly ITahsilatOdemeBelgesiService _service;
    private readonly IMapper _mapper;

    public TahsilatOdemeBelgeleriController(ITahsilatOdemeBelgesiService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<List<TahsilatOdemeBelgesiDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _service.WhereAsync(x => x.CariKart != null && x.CariKart.TesisId == tesisId.Value)
            : await _service.GetAllAsync();

        return Ok(items.OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<PagedResult<TahsilatOdemeBelgesiDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: tesisId.HasValue && tesisId.Value > 0 ? x => x.CariKart != null && x.CariKart.TesisId == tesisId.Value : null,
            orderBy: q => q.OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<TahsilatOdemeBelgesiDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("gunluk-ozet")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<TahsilatOdemeOzetDto>> GetGunlukOzet([FromQuery] DateTime? gun, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetGunlukOzetAsync(gun?.Date ?? DateTime.Today, tesisId, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.Manage)]
    public async Task<ActionResult<TahsilatOdemeBelgesiDto>> Create([FromBody] CreateTahsilatOdemeBelgesiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<TahsilatOdemeBelgesiDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.Manage)]
    public async Task<ActionResult<TahsilatOdemeBelgesiDto>> Update(int id, [FromBody] UpdateTahsilatOdemeBelgesiRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TahsilatOdemeBelgesiDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Controllers;

[Route("api/muhasebe/tahsilat-odeme-belgeleri")]
[ApiController]
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
    public async Task<ActionResult<List<TahsilatOdemeBelgesiDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<PagedResult<TahsilatOdemeBelgesiDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderByDescending(x => x.BelgeTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<TahsilatOdemeBelgesiDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("gunluk-ozet")]
    [Permission(StructurePermissions.TahsilatOdemeBelgesiYonetimi.View)]
    public async Task<ActionResult<TahsilatOdemeOzetDto>> GetGunlukOzet([FromQuery] DateTime? gun, CancellationToken cancellationToken)
        => Ok(await _service.GetGunlukOzetAsync(gun?.Date ?? DateTime.Today, cancellationToken));

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

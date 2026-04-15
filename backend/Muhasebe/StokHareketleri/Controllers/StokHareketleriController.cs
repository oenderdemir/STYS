using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.StokHareketleri.Controllers;

[Route("api/muhasebe/stok-hareketleri")]
[ApiController]
public class StokHareketleriController : UIController
{
    private readonly IStokHareketService _service;
    private readonly IMapper _mapper;

    public StokHareketleriController(IStokHareketService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokHareketDto>>> GetList([FromQuery] int? depoId, CancellationToken cancellationToken)
    {
        var items = await _service.GetAllAsync();
        var query = items.AsQueryable();
        if (depoId.HasValue && depoId.Value > 0)
        {
            query = query.Where(x => x.DepoId == depoId.Value);
        }

        return Ok(query.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<StokHareketDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? depoId, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPagedAsync(
            request,
            predicate: depoId.HasValue && depoId.Value > 0 ? x => x.DepoId == depoId.Value : null,
            orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<StokHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("stok-bakiye")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokBakiyeDto>>> GetStokBakiye([FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetStokBakiyeAsync(depoId, cancellationToken));

    [HttpGet("stok-kart-ozet")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokKartOzetDto>>> GetStokKartOzet([FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetStokKartOzetAsync(depoId, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokHareketDto>> Create([FromBody] CreateStokHareketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<StokHareketDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokHareketDto>> Update(int id, [FromBody] UpdateStokHareketRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<StokHareketDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

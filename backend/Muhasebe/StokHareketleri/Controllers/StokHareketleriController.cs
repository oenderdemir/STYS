using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.StokHareketleri.Controllers;

[Route("ui/muhasebe/stok-hareketleri")]
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
    public async Task<ActionResult<List<StokHareketDto>>> GetList([FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _service.WhereAsync(x => x.Depo != null && x.Depo.TesisId == tesisId.Value)
            : await _service.GetAllAsync();

        var query = items.AsQueryable();
        if (depoId.HasValue && depoId.Value > 0)
        {
            query = query.Where(x => x.DepoId == depoId.Value);
        }

        return Ok(query.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<StokHareketDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: BuildPredicate(tesisId, depoId),
            orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<StokHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("stok-bakiye")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokBakiyeDto>>> GetStokBakiye([FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetStokBakiyeAsync(tesisId, depoId, cancellationToken));

    [HttpGet("stok-kart-ozet")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokKartOzetDto>>> GetStokKartOzet([FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetStokKartOzetAsync(tesisId, depoId, cancellationToken));

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

    private static System.Linq.Expressions.Expression<Func<STYS.Muhasebe.StokHareketleri.Entities.StokHareket, bool>>? BuildPredicate(int? tesisId, int? depoId)
        => tesisId.HasValue && tesisId.Value > 0 || depoId.HasValue && depoId.Value > 0
            ? x =>
                (!tesisId.HasValue || tesisId <= 0 || (x.Depo != null && x.Depo.TesisId == tesisId.Value)) &&
                (!depoId.HasValue || depoId <= 0 || x.DepoId == depoId.Value)
            : null;
}

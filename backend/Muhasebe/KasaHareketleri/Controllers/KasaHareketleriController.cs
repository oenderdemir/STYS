using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.KasaHareketleri.Controllers;

[Route("ui/muhasebe/kasa-hareketleri")]
public class KasaHareketleriController : UIController
{
    private readonly IKasaHareketService _service;
    private readonly IMapper _mapper;

    public KasaHareketleriController(IKasaHareketService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.KasaHareketYonetimi.View)]
    public async Task<ActionResult<List<KasaHareketDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _service.WhereAsync(x => x.KasaBankaHesap != null && x.KasaBankaHesap.TesisId == tesisId.Value)
            : await _service.GetAllAsync();

        return Ok(items.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.KasaHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<KasaHareketDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: tesisId.HasValue && tesisId.Value > 0 ? x => x.KasaBankaHesap != null && x.KasaBankaHesap.TesisId == tesisId.Value : null,
            orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KasaHareketYonetimi.View)]
    public async Task<ActionResult<KasaHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.KasaHareketYonetimi.Manage)]
    public async Task<ActionResult<KasaHareketDto>> Create([FromBody] CreateKasaHareketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<KasaHareketDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KasaHareketYonetimi.Manage)]
    public async Task<ActionResult<KasaHareketDto>> Update(int id, [FromBody] UpdateKasaHareketRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<KasaHareketDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.KasaHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

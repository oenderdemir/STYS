using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.KasaHareketleri.Controllers;

[Route("api/muhasebe/kasa-hareketleri")]
[ApiController]
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
    public async Task<ActionResult<List<KasaHareketDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.KasaHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<KasaHareketDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));

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

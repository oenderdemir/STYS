using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.CariHareketler.Controllers;

[Route("api/muhasebe/cari-hareketler")]
[ApiController]
public class CariHareketlerController : UIController
{
    private readonly ICariHareketService _service;
    private readonly IMapper _mapper;

    public CariHareketlerController(ICariHareketService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<List<CariHareketDto>>> GetList([FromQuery] int? cariKartId, CancellationToken cancellationToken)
    {
        var items = await _service.GetAllAsync();
        var query = items.AsQueryable();
        if (cariKartId.HasValue && cariKartId.Value > 0)
        {
            query = query.Where(x => x.CariKartId == cariKartId.Value);
        }

        return Ok(query.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<CariHareketDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? cariKartId, CancellationToken cancellationToken)
    {
        return Ok(await _service.GetPagedAsync(
            request,
            predicate: cariKartId.HasValue && cariKartId.Value > 0 ? x => x.CariKartId == cariKartId.Value : null,
            orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<CariHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("cari/{cariKartId:int}/ekstre")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<CariEkstreDto>> GetEkstre(int cariKartId, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis, CancellationToken cancellationToken)
        => Ok(await _service.GetEkstreAsync(cariKartId, baslangic, bitis, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<ActionResult<CariHareketDto>> Create([FromBody] CreateCariHareketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<CariHareketDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<ActionResult<CariHareketDto>> Update(int id, [FromBody] UpdateCariHareketRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<CariHareketDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

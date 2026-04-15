using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.BankaHareketleri.Dtos;
using STYS.Muhasebe.BankaHareketleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.BankaHareketleri.Controllers;

[Route("api/muhasebe/banka-hareketleri")]
[ApiController]
public class BankaHareketleriController : UIController
{
    private readonly IBankaHareketService _service;
    private readonly IMapper _mapper;

    public BankaHareketleriController(IBankaHareketService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.BankaHareketYonetimi.View)]
    public async Task<ActionResult<List<BankaHareketDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.BankaHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<BankaHareketDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.BankaHareketYonetimi.View)]
    public async Task<ActionResult<BankaHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.BankaHareketYonetimi.Manage)]
    public async Task<ActionResult<BankaHareketDto>> Create([FromBody] CreateBankaHareketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<BankaHareketDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.BankaHareketYonetimi.Manage)]
    public async Task<ActionResult<BankaHareketDto>> Update(int id, [FromBody] UpdateBankaHareketRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<BankaHareketDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.BankaHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

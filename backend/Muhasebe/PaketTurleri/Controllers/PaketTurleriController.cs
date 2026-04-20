using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.PaketTurleri.Dtos;
using STYS.Muhasebe.PaketTurleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.PaketTurleri.Controllers;

[Route("api/muhasebe/paket-turleri")]
[ApiController]
public class PaketTurleriController : UIController
{
    private readonly IPaketTuruService _service;
    private readonly IMapper _mapper;

    public PaketTurleriController(IPaketTuruService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.PaketTuruYonetimi.View)]
    public async Task<ActionResult<List<PaketTuruDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderBy(x => x.Ad).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.PaketTuruYonetimi.View)]
    public async Task<ActionResult<PagedResult<PaketTuruDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.PaketTuruYonetimi.View)]
    public async Task<ActionResult<PaketTuruDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.PaketTuruYonetimi.Manage)]
    public async Task<ActionResult<PaketTuruDto>> Create([FromBody] CreatePaketTuruRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<PaketTuruDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.PaketTuruYonetimi.Manage)]
    public async Task<ActionResult<PaketTuruDto>> Update(int id, [FromBody] UpdatePaketTuruRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<PaketTuruDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.PaketTuruYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

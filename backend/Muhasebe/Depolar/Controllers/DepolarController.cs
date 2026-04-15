using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.Depolar.Controllers;

[Route("api/muhasebe/depolar")]
[ApiController]
public class DepolarController : UIController
{
    private readonly IDepoService _service;
    private readonly IMapper _mapper;

    public DepolarController(IDepoService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.DepoYonetimi.View)]
    public async Task<ActionResult<List<DepoDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderBy(x => x.Kod).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.DepoYonetimi.View)]
    public async Task<ActionResult<PagedResult<DepoDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.Kod).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.DepoYonetimi.View)]
    public async Task<ActionResult<DepoDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.DepoYonetimi.Manage)]
    public async Task<ActionResult<DepoDto>> Create([FromBody] CreateDepoRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<DepoDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.DepoYonetimi.Manage)]
    public async Task<ActionResult<DepoDto>> Update(int id, [FromBody] UpdateDepoRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<DepoDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.DepoYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

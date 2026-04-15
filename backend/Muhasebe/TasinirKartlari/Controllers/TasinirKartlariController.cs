using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.TasinirKartlari.Controllers;

[Route("api/muhasebe/tasinir-kartlari")]
[ApiController]
public class TasinirKartlariController : UIController
{
    private readonly ITasinirKartService _service;
    private readonly IMapper _mapper;

    public TasinirKartlariController(ITasinirKartService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.TasinirKartYonetimi.View)]
    public async Task<ActionResult<List<TasinirKartDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderBy(x => x.StokKodu).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.TasinirKartYonetimi.View)]
    public async Task<ActionResult<PagedResult<TasinirKartDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.StokKodu).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TasinirKartYonetimi.View)]
    public async Task<ActionResult<TasinirKartDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.TasinirKartYonetimi.Manage)]
    public async Task<ActionResult<TasinirKartDto>> Create([FromBody] CreateTasinirKartRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<TasinirKartDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TasinirKartYonetimi.Manage)]
    public async Task<ActionResult<TasinirKartDto>> Update(int id, [FromBody] UpdateTasinirKartRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TasinirKartDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TasinirKartYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

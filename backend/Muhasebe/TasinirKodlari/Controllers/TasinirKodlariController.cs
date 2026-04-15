using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.TasinirKodlari.Dtos;
using STYS.Muhasebe.TasinirKodlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.TasinirKodlari.Controllers;

[Route("api/muhasebe/tasinir-kodlari")]
[ApiController]
public class TasinirKodlariController : UIController
{
    private readonly ITasinirKodService _service;
    private readonly IMapper _mapper;

    public TasinirKodlariController(ITasinirKodService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.TasinirKodYonetimi.View)]
    public async Task<ActionResult<List<TasinirKodDto>>> GetList(CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync()).OrderBy(x => x.TamKod).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.TasinirKodYonetimi.View)]
    public async Task<ActionResult<PagedResult<TasinirKodDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery(Name = "q")] string? query, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedForLookupAsync(request, query, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TasinirKodYonetimi.View)]
    public async Task<ActionResult<TasinirKodDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.TasinirKodYonetimi.Manage)]
    public async Task<ActionResult<TasinirKodDto>> Create([FromBody] CreateTasinirKodRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<TasinirKodDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TasinirKodYonetimi.Manage)]
    public async Task<ActionResult<TasinirKodDto>> Update(int id, [FromBody] UpdateTasinirKodRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<TasinirKodDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TasinirKodYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("import")]
    [Permission(StructurePermissions.TasinirKodYonetimi.Manage)]
    public async Task<ActionResult<TasinirKodImportSonucDto>> Import([FromBody] ImportTasinirKodlariRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ImportAsync(request, cancellationToken));
}

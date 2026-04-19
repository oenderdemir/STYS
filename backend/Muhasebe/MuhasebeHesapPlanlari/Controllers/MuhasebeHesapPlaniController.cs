using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Controllers;

[Route("api/muhasebe/hesap-plani")]
[ApiController]
public class MuhasebeHesapPlaniController : UIController
{
    private readonly IMuhasebeHesapPlaniService _service;
    private readonly IMapper _mapper;

    public MuhasebeHesapPlaniController(IMuhasebeHesapPlaniService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetList(CancellationToken cancellationToken)
        => Ok(await _service.GetTreeAsync(cancellationToken));

    [HttpGet("tree")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTree(CancellationToken cancellationToken)
        => Ok(await _service.GetTreeAsync(cancellationToken));

    [HttpGet("tree/roots")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTreeRoots(CancellationToken cancellationToken)
        => Ok(await _service.GetTreeRootsAsync(cancellationToken));

    [HttpGet("tree/children")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTreeChildren([FromQuery] int? parentId, CancellationToken cancellationToken)
        => Ok(await _service.GetTreeChildrenAsync(parentId, cancellationToken));

    [HttpGet("paged")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<PagedResult<MuhasebeHesapPlaniDto>>> GetPaged([FromQuery] PagedRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, orderBy: q => q.OrderBy(x => x.TamKod).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> Create([FromBody] CreateMuhasebeHesapPlaniRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<MuhasebeHesapPlaniDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> Update(int id, [FromBody] UpdateMuhasebeHesapPlaniRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeHesapPlaniDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

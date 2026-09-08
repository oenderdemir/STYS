using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Controllers;

[Route("ui/muhasebe/hesap-plani")]
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
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetTreeAsync(tesisId, cancellationToken));

    [HttpGet("tree")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTree([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetTreeAsync(tesisId, cancellationToken));

    [HttpGet("tree/roots")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTreeRoots([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetTreeRootsAsync(tesisId, cancellationToken));

    [HttpGet("tree/children")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<List<MuhasebeHesapPlaniDto>>> GetTreeChildren([FromQuery] int? parentId, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetTreeChildrenAsync(parentId, tesisId, cancellationToken));

    [HttpGet("paged")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<PagedResult<MuhasebeHesapPlaniDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, tesisId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.View)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> GetById(int id, [FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, tesisId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> Create([FromBody] CreateMuhasebeHesapPlaniRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<MuhasebeHesapPlaniDto>(request)));

    [HttpPost("{anaHesapId:int}/detay-hesap")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> CreateDetayHesap(
        int anaHesapId,
        [FromQuery] int? tesisId,
        [FromBody] MuhasebeDetayHesapOlusturRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.CreateDetayHesapAsync(anaHesapId, request?.Ad ?? string.Empty, tesisId, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<ActionResult<MuhasebeHesapPlaniDto>> Update(int id, [FromQuery] int? tesisId, [FromBody] UpdateMuhasebeHesapPlaniRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<MuhasebeHesapPlaniDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, tesisId, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.MuhasebeHesapPlaniYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, tesisId, cancellationToken);
        return Ok();
    }
}

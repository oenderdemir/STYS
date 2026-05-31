using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.CariKartlar.Controllers;

[Route("ui/muhasebe/cari-kartlar")]
public class CariKartlarController : UIController
{
    private readonly ICariKartService _service;
    private readonly IMapper _mapper;

    public CariKartlarController(ICariKartService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.CariKartYonetimi.View)]
    public async Task<ActionResult<List<CariKartDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok((await _service.GetAllAsync(tesisId)).OrderBy(x => x.CariKodu).ThenBy(x => x.Id).ToList());

    [HttpGet("paged")]
    [Permission(StructurePermissions.CariKartYonetimi.View)]
    public async Task<ActionResult<PagedResult<CariKartDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(request, tesisId, orderBy: q => q.OrderBy(x => x.CariKodu).ThenBy(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.CariKartYonetimi.View)]
    public async Task<ActionResult<CariKartDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{cariKartId:int}/bakiye")]
    [Permission(StructurePermissions.CariKartYonetimi.View)]
    public async Task<ActionResult<CariBakiyeDto>> GetBakiye(int cariKartId, CancellationToken cancellationToken)
        => Ok(await _service.GetBakiyeAsync(cariKartId, cancellationToken));

    [HttpPost("{id:int}/acilis-bakiyesi-duzelt")]
    [Permission(StructurePermissions.CariKartYonetimi.Manage)]
    public async Task<ActionResult<CariKartDto>> AcilisBakiyesiDuzelt(
        int id,
        [FromBody] CariKartAcilisBakiyesiDuzeltRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.CariKartAcilisBakiyesiDuzeltAsync(
            id,
            request.YeniTutar,
            request.YeniYonu,
            request.DuzeltmeTarihi,
            cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.CariKartYonetimi.Manage)]
    public async Task<ActionResult<CariKartDto>> Create([FromBody] CreateCariKartRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<CariKartDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.CariKartYonetimi.Manage)]
    public async Task<ActionResult<CariKartDto>> Update(int id, [FromBody] UpdateCariKartRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<CariKartDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.CariKartYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }
}

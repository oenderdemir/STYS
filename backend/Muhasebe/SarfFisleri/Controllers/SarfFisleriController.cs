using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;
using STYS.Muhasebe.SarfFisleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.SarfFisleri.Controllers;

[Route("ui/muhasebe/sarf-fisleri")]
public class SarfFisleriController : UIController
{
    private readonly ISarfFisiService _service;
    private readonly IMapper _mapper;

    public SarfFisleriController(ISarfFisiService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<PagedResult<SarfFisiDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: BuildPredicate(tesisId, depoId),
            orderBy: q => q.OrderByDescending(x => x.SarfTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<SarfFisiDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("birimler")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<List<SarfBirimSecenekDto>>> GetBirimler([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetBirimlerAsync(tesisId, cancellationToken));

    [HttpGet("odalar")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<List<SarfOdaSecenekDto>>> GetOdalar([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetOdalarAsync(tesisId, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.SarfYonetimi.Create)]
    public async Task<ActionResult<SarfFisiDto>> Create([FromBody] CreateSarfFisiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<SarfFisiDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.SarfYonetimi.Create)]
    public async Task<ActionResult<SarfFisiDto>> Update(int id, [FromBody] CreateSarfFisiRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<SarfFisiDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpPut("{id:int}/satirlar")]
    [Permission(StructurePermissions.SarfYonetimi.Create)]
    public async Task<ActionResult<SarfFisiDto>> UpdateSatirlar(int id, [FromBody] UpdateSarfFisiSatirlarRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateSatirlarAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/satirlar")]
    [Permission(StructurePermissions.SarfYonetimi.Create)]
    public async Task<ActionResult<SarfFisiDto>> AddSatir(int id, [FromBody] AddSarfFisiSatirRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddSatirAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}/satirlar/{satirId:int}")]
    [Permission(StructurePermissions.SarfYonetimi.Create)]
    public async Task<IActionResult> DeleteSatir(int id, int satirId, CancellationToken cancellationToken)
    {
        await _service.DeleteSatirAsync(id, satirId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/kesinlestir")]
    [Permission(StructurePermissions.SarfYonetimi.Finalize)]
    public async Task<ActionResult<SarfFisiDto>> Kesinlestir(int id, CancellationToken cancellationToken)
        => Ok(await _service.KesinlestirAsync(id, cancellationToken));

    [HttpPost("{id:int}/iptal")]
    [Permission(StructurePermissions.SarfYonetimi.Cancel)]
    public async Task<ActionResult<SarfFisiDto>> Iptal(int id, [FromBody] IptalSarfFisiRequest? request, CancellationToken cancellationToken)
        => Ok(await _service.IptalAsync(id, request?.IptalAciklamasi, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.SarfYonetimi.Cancel)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    private static System.Linq.Expressions.Expression<Func<SarfFisi, bool>>? BuildPredicate(int? tesisId, int? depoId)
        => tesisId.HasValue && tesisId.Value > 0 || depoId.HasValue && depoId.Value > 0
            ? x =>
                (!tesisId.HasValue || tesisId <= 0 || x.TesisId == tesisId.Value) &&
                (!depoId.HasValue || depoId <= 0 || x.DepoId == depoId.Value)
            : null;
}

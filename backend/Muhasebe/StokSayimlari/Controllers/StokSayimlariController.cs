using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokSayimlari.Dtos;
using STYS.Muhasebe.StokSayimlari.Entities;
using STYS.Muhasebe.StokSayimlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.StokSayimlari.Controllers;

[Route("ui/muhasebe/stok-sayimlari")]
public class StokSayimlariController : UIController
{
    private readonly IStokSayimService _service;
    private readonly IMapper _mapper;

    public StokSayimlariController(IStokSayimService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<StokSayimDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, [FromQuery] int? depoId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: BuildPredicate(tesisId, depoId),
            orderBy: q => q.OrderByDescending(x => x.SayimTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<StokSayimDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> Create([FromBody] CreateStokSayimRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<StokSayimDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> Update(int id, [FromBody] CreateStokSayimRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<StokSayimDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpPut("{id:int}/satirlar")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> UpdateSatirlar(int id, [FromBody] UpdateStokSayimSatirlarRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateSatirlarAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/satirlar")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> AddSatir(int id, [FromBody] AddStokSayimSatirRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddSatirAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}/satirlar/{satirId:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<IActionResult> DeleteSatir(int id, int satirId, CancellationToken cancellationToken)
    {
        await _service.DeleteSatirAsync(id, satirId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/refresh")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> Refresh(int id, CancellationToken cancellationToken)
        => Ok(await _service.RefreshAsync(id, cancellationToken));

    [HttpPost("{id:int}/kesinlestir")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> Kesinlestir(int id, CancellationToken cancellationToken)
        => Ok(await _service.KesinlestirAsync(id, cancellationToken));

    [HttpPost("{id:int}/iptal")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<ActionResult<StokSayimDto>> Iptal(int id, CancellationToken cancellationToken)
        => Ok(await _service.IptalAsync(id, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.StokHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    private static System.Linq.Expressions.Expression<Func<StokSayim, bool>>? BuildPredicate(int? tesisId, int? depoId)
        => tesisId.HasValue && tesisId.Value > 0 || depoId.HasValue && depoId.Value > 0
            ? x =>
                (!tesisId.HasValue || tesisId <= 0 || x.TesisId == tesisId.Value) &&
                (!depoId.HasValue || depoId <= 0 || x.DepoId == depoId.Value)
            : null;
}

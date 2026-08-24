using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokCikis.Services;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Entities;
using STYS.Muhasebe.StokTalepleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.StokTalepleri.Controllers;

[Route("ui/muhasebe/stok-talepleri")]
public class StokTalepleriController : UIController
{
    private readonly IStokTalepService _service;
    private readonly IStokCikisService _stokCikisService;
    private readonly IMapper _mapper;

    public StokTalepleriController(IStokTalepService service, IStokCikisService stokCikisService, IMapper mapper)
    {
        _service = service;
        _stokCikisService = stokCikisService;
        _mapper = mapper;
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.StokTalepYonetimi.View)]
    public async Task<ActionResult<PagedResult<StokTalepDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, [FromQuery] int? talepEdenDepoId, [FromQuery] int? karsilayanDepoId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: BuildPredicate(tesisId, talepEdenDepoId, karsilayanDepoId),
            orderBy: q => q.OrderByDescending(x => x.TalepTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.StokTalepYonetimi.View)]
    public async Task<ActionResult<StokTalepDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<ActionResult<StokTalepDto>> Create([FromBody] CreateStokTalepRequest request, CancellationToken cancellationToken)
        => Ok(await _stokCikisService.TalepBaslatAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<ActionResult<StokTalepDto>> Update(int id, [FromBody] CreateStokTalepRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<StokTalepDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpPut("{id:int}/talep-satirlari")]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<ActionResult<StokTalepDto>> UpdateTalepSatirlari(int id, [FromBody] UpdateTalepSatirlariRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateTalepSatirlariAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/onay-miktarlari")]
    [Permission(StructurePermissions.StokTalepYonetimi.Approve)]
    public async Task<ActionResult<StokTalepDto>> OnayMiktarlariniGuncelle(int id, [FromBody] OnayMiktarlariniGuncelleRequest request, CancellationToken cancellationToken)
        => Ok(await _service.OnayMiktarlariniGuncelleAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/satirlar")]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<ActionResult<StokTalepDto>> AddSatir(int id, [FromBody] AddStokTalepSatirRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddSatirAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}/satirlar/{satirId:int}")]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<IActionResult> DeleteSatir(int id, int satirId, CancellationToken cancellationToken)
    {
        await _service.DeleteSatirAsync(id, satirId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/gonder")]
    [Permission(StructurePermissions.StokTalepYonetimi.Create)]
    public async Task<ActionResult<StokTalepDto>> Gonder(int id, CancellationToken cancellationToken)
        => Ok(await _service.GonderAsync(id, cancellationToken));

    [HttpPost("{id:int}/reddet")]
    [Permission(StructurePermissions.StokTalepYonetimi.Approve)]
    public async Task<ActionResult<StokTalepDto>> Reddet(int id, CancellationToken cancellationToken)
        => Ok(await _service.ReddetAsync(id, cancellationToken));

    [HttpPost("{id:int}/teslim-et")]
    [Permission(StructurePermissions.StokTalepYonetimi.Deliver)]
    public async Task<ActionResult<StokTalepDto>> TeslimEt(int id, [FromBody] TeslimEtStokTalepRequest request, CancellationToken cancellationToken)
        => Ok(await _service.TeslimEtAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/iptal")]
    [Permission(StructurePermissions.StokTalepYonetimi.Cancel)]
    public async Task<ActionResult<StokTalepDto>> Iptal(int id, CancellationToken cancellationToken)
        => Ok(await _service.IptalAsync(id, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.StokTalepYonetimi.Cancel)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    private static System.Linq.Expressions.Expression<Func<StokTalep, bool>>? BuildPredicate(int? tesisId, int? talepEdenDepoId, int? karsilayanDepoId)
        => tesisId.HasValue && tesisId.Value > 0 || talepEdenDepoId.HasValue && talepEdenDepoId.Value > 0 || karsilayanDepoId.HasValue && karsilayanDepoId.Value > 0
            ? x =>
                (!tesisId.HasValue || tesisId <= 0 || x.TesisId == tesisId.Value) &&
                (!talepEdenDepoId.HasValue || talepEdenDepoId <= 0 || x.TalepEdenDepoId == talepEdenDepoId.Value) &&
                (!karsilayanDepoId.HasValue || karsilayanDepoId <= 0 || x.KarsilayanDepoId == karsilayanDepoId.Value)
            : null;
}

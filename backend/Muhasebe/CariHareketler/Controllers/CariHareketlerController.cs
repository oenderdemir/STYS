using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.CariHareketler.Controllers;

[Route("ui/muhasebe/cari-hareketler")]
public class CariHareketlerController : UIController
{
    private readonly ICariHareketService _service;
    private readonly IMapper _mapper;

    public CariHareketlerController(ICariHareketService service, IMapper mapper)
    {
        _service = service;
        _mapper = mapper;
    }

    [HttpGet]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<List<CariHareketDto>>> GetList([FromQuery] int? tesisId, [FromQuery] int? cariKartId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _service.WhereAsync(x => x.CariKart != null && x.CariKart.TesisId == tesisId.Value)
            : await _service.GetAllAsync();

        var query = items.AsQueryable();
        if (cariKartId.HasValue && cariKartId.Value > 0)
        {
            query = query.Where(x => x.CariKartId == cariKartId.Value);
        }

        return Ok(query.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id).ToList());
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<PagedResult<CariHareketDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, [FromQuery] int? cariKartId, CancellationToken cancellationToken)
        => Ok(await _service.GetPagedAsync(
            request,
            predicate: BuildPredicate(tesisId, cariKartId),
            orderBy: q => q.OrderByDescending(x => x.HareketTarihi).ThenByDescending(x => x.Id)));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<CariHareketDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("cari/{cariKartId:int}/ekstre")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<CariEkstreDto>> GetEkstre(int cariKartId, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis, CancellationToken cancellationToken)
        => Ok(await _service.GetEkstreAsync(cariKartId, baslangic, bitis, cancellationToken));

    [HttpGet("cari/{cariKartId:int}/bakiye-ozet")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<CariBakiyeOzetDto>> GetBakiyeOzet(int cariKartId, CancellationToken cancellationToken)
        => Ok(await _service.GetCariBakiyeOzetAsync(cariKartId, cancellationToken));

    [HttpGet("cari/{cariKartId:int}/acik-hareketler")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<List<CariHareketDurumOzetDto>>> GetAcikHareketler(int cariKartId, CancellationToken cancellationToken)
        => Ok(await _service.GetCariAcikHareketlerAsync(cariKartId, cancellationToken));

    [HttpGet("cari/{cariKartId:int}/kapanan-hareketler")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<List<CariHareketDurumOzetDto>>> GetKapananHareketler(int cariKartId, CancellationToken cancellationToken)
        => Ok(await _service.GetCariKapananHareketlerAsync(cariKartId, cancellationToken));

    [HttpGet("cari/{cariKartId:int}/hareket-ekstre")]
    [Permission(StructurePermissions.CariHareketYonetimi.View)]
    public async Task<ActionResult<List<CariHareketDurumOzetDto>>> GetHareketEkstre(int cariKartId, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis, CancellationToken cancellationToken)
        => Ok(await _service.GetCariHareketEkstreAsync(cariKartId, baslangic, bitis, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<ActionResult<CariHareketDto>> Create([FromBody] CreateCariHareketRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(_mapper.Map<CariHareketDto>(request)));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<ActionResult<CariHareketDto>> Update(int id, [FromBody] UpdateCariHareketRequest request, CancellationToken cancellationToken)
    {
        var dto = _mapper.Map<CariHareketDto>(request);
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.CariHareketYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    private static System.Linq.Expressions.Expression<Func<STYS.Muhasebe.CariHareketler.Entities.CariHareket, bool>>? BuildPredicate(int? tesisId, int? cariKartId)
        => tesisId.HasValue && tesisId.Value > 0 || cariKartId.HasValue && cariKartId.Value > 0
            ? x =>
                (!tesisId.HasValue || tesisId <= 0 || (x.CariKart != null && x.CariKart.TesisId == tesisId.Value)) &&
                (!cariKartId.HasValue || cariKartId <= 0 || x.CariKartId == cariKartId.Value)
            : null;
}

using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.PosTahsilatValorleri.Controllers;

[Route("ui/muhasebe/pos-tahsilat-valor")]
public class PosTahsilatValorController : UIController
{
    private readonly IPosTahsilatValorService _service;
    private readonly IPosTahsilatValorAktarimService _aktarimService;

    public PosTahsilatValorController(IPosTahsilatValorService service, IPosTahsilatValorAktarimService aktarimService)
    {
        _service = service;
        _aktarimService = aktarimService;
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.View)]
    public async Task<ActionResult<PagedResult<PosTahsilatValorDto>>> GetPaged([FromQuery] PagedRequest request, [FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        System.Linq.Expressions.Expression<Func<PosTahsilatValor, bool>>? predicate = tesisId.HasValue
            ? x => x.TesisId == tesisId.Value
            : null;
        return Ok(await _service.GetPagedAsync(request, predicate, orderBy: q => q.OrderByDescending(x => x.BeklenenValorTarihi).ThenBy(x => x.Id)));
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.View)]
    public async Task<ActionResult<PosTahsilatValorDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("ozet")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.View)]
    public async Task<ActionResult<PosTahsilatValorOzetDto>> GetOzet([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetOzetAsync(tesisId, cancellationToken));

    [HttpPost("toplu-onay-bilgisi")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.View)]
    public async Task<ActionResult<PosTahsilatValorTopluOnayBilgisiDto>> GetTopluOnayBilgisi([FromBody] PosTahsilatValorTopluOnayBilgisiRequest request, CancellationToken cancellationToken)
        => Ok(await _service.GetTopluOnayBilgisiAsync(request, cancellationToken));

    [HttpPost("{id:int}/hesaba-aktar")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.Manage)]
    public async Task<ActionResult<PosTahsilatValorAktarimSonucDto>> HesabaAktar(int id, [FromBody] ManuelAktarimGuncellemeDto? guncelleme, CancellationToken cancellationToken)
        => Ok(await _aktarimService.HesabaAktarAsync(id, guncelleme, cancellationToken));

    [HttpPost("secili-hesaplara-aktar")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.Manage)]
    public async Task<ActionResult<PosTahsilatValorToplamAktarimSonucDto>> SeciliHesaplaraAktar([FromBody] List<int> valorIdler, CancellationToken cancellationToken)
        => Ok(await _aktarimService.SeciliHesaplaraAktarAsync(valorIdler, cancellationToken));

    [HttpPost("valoru-gelenleri-hesaba-aktar")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.Manage)]
    public async Task<ActionResult<PosTahsilatValorToplamAktarimSonucDto>> ValoruGelenleriHesabaAktar([FromQuery] int? tesisId, CancellationToken cancellationToken)
        => Ok(await _aktarimService.ValoruGelenleriHesabaAktarAsync(tesisId, cancellationToken));

    [HttpPost("{id:int}/yeniden-dene")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.Manage)]
    public async Task<ActionResult<PosTahsilatValorAktarimSonucDto>> YenidenDene(int id, CancellationToken cancellationToken)
        => Ok(await _aktarimService.YenidenDeneAsync(id, cancellationToken));

    [HttpPost("{id:int}/duzeltme-ters-kayit")]
    [Permission(StructurePermissions.PosTahsilatValorYonetimi.Manage)]
    public async Task<ActionResult<PosTahsilatValorAktarimSonucDto>> DuzeltmeTersKayit(int id, [FromBody] DuzeltmeTersKayitRequest request, CancellationToken cancellationToken)
        => Ok(await _aktarimService.DuzeltmeTersKayitAsync(id, request.Aciklama, cancellationToken));
}

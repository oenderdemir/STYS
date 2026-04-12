using Microsoft.AspNetCore.Mvc;
using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranOdemeleri.Services;
using STYS.RestoranSiparisleri.Dtos;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranOdemeleri.Controllers;

[Route("api/restoran-odemeleri")]
[ApiController]
public class RestoranOdemeleriController : UIController
{
    private readonly IRestoranOdemeService _service;

    public RestoranOdemeleriController(IRestoranOdemeService service)
    {
        _service = service;
    }

    [HttpGet("/api/restoran-siparisleri/{siparisId:int}/odemeler")]
    [Permission(StructurePermissions.RestoranOdemeYonetimi.View)]
    public async Task<ActionResult<List<RestoranOdemeDto>>> GetSiparisOdemeleri(int siparisId, CancellationToken cancellationToken)
        => Ok(await _service.GetBySiparisIdAsync(siparisId, cancellationToken));

    [HttpGet("/api/restoran-siparisleri/{siparisId:int}/odeme-ozeti")]
    [Permission(StructurePermissions.RestoranOdemeYonetimi.View)]
    public async Task<ActionResult<RestoranSiparisOdemeOzetiDto>> GetOdemeOzeti(int siparisId, CancellationToken cancellationToken)
        => Ok(await _service.GetOdemeOzetiAsync(siparisId, cancellationToken));

    [HttpPost("/api/restoran-siparisleri/{siparisId:int}/odemeler/nakit")]
    [Permission(StructurePermissions.RestoranOdemeYonetimi.Manage)]
    public async Task<ActionResult<RestoranOdemeDto>> NakitOdeme(int siparisId, [FromBody] CreateNakitOdemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateNakitOdemeAsync(siparisId, request, cancellationToken));

    [HttpPost("/api/restoran-siparisleri/{siparisId:int}/odemeler/kredi-karti")]
    [Permission(StructurePermissions.RestoranOdemeYonetimi.Manage)]
    public async Task<ActionResult<RestoranOdemeDto>> KrediKartiOdeme(int siparisId, [FromBody] CreateKrediKartiOdemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateKrediKartiOdemeAsync(siparisId, request, cancellationToken));

    [HttpPost("/api/restoran-siparisleri/{siparisId:int}/odemeler/odaya-ekle")]
    [Permission(StructurePermissions.RestoranOdemeYonetimi.Manage)]
    public async Task<ActionResult<RestoranOdemeDto>> OdayaEkle(int siparisId, [FromBody] CreateOdayaEkleOdemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateOdayaEkleOdemeAsync(siparisId, request, cancellationToken));
}

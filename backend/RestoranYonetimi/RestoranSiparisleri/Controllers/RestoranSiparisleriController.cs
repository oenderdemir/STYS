using Microsoft.AspNetCore.Mvc;
using STYS.RestoranSiparisleri.Dtos;
using STYS.RestoranSiparisleri.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.RestoranSiparisleri.Controllers;

[Route("api/restoran-siparisleri")]
[ApiController]
public class RestoranSiparisleriController : UIController
{
    private readonly IRestoranSiparisService _service;

    public RestoranSiparisleriController(IRestoranSiparisService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.View)]
    public async Task<ActionResult<List<RestoranSiparisDto>>> GetList([FromQuery] int? restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(restoranId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.View)]
    public async Task<ActionResult<RestoranSiparisDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.Manage)]
    public async Task<ActionResult<RestoranSiparisDto>> Create([FromBody] CreateRestoranSiparisRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.Manage)]
    public async Task<ActionResult<RestoranSiparisDto>> Update(int id, [FromBody] UpdateRestoranSiparisRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/durum")]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.Manage)]
    public async Task<ActionResult<RestoranSiparisDto>> UpdateDurum(int id, [FromBody] UpdateRestoranSiparisDurumRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateDurumAsync(id, request, cancellationToken));

    [HttpGet("/api/restoranlar/{restoranId:int}/siparisler")]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.View)]
    public async Task<ActionResult<List<RestoranSiparisDto>>> GetByRestoranId(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetByRestoranIdAsync(restoranId, cancellationToken));

    [HttpGet("acik")]
    [Permission(StructurePermissions.RestoranSiparisYonetimi.View)]
    public async Task<ActionResult<List<RestoranSiparisDto>>> GetAcikSiparisler([FromQuery] int? masaId, CancellationToken cancellationToken)
        => Ok(await _service.GetAcikSiparislerAsync(masaId, cancellationToken));
}

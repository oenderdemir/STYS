using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Services;
using System.Linq;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Restoranlar.Controllers;

[Route("api/restoranlar")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Restoran)]
public class RestoranlarController : UIController
{
    private readonly IRestoranService _service;

    public RestoranlarController(IRestoranService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.RestoranYonetimi.View)]
    public async Task<ActionResult<List<RestoranDto>>> GetList([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var items = tesisId.HasValue && tesisId.Value > 0
            ? await _service.WhereAsync(x => x.TesisId == tesisId.Value)
            : await _service.GetAllAsync();
        return Ok(items.ToList());
    }

    [HttpGet("isletme-alanlari")]
    [Permission(StructurePermissions.RestoranYonetimi.View)]
    public async Task<ActionResult<List<RestoranIsletmeAlaniSecenekDto>>> GetIsletmeAlaniSecenekleri([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetIsletmeAlaniSecenekleriAsync(tesisId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.View)]
    public async Task<ActionResult<RestoranDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<ActionResult<RestoranDto>> Create([FromBody] CreateRestoranRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranDto
        {
            TesisId = request.TesisId,
            IsletmeAlaniId = request.IsletmeAlaniId,
            YoneticiUserIds = request.YoneticiUserIds,
            GarsonUserIds = request.GarsonUserIds,
            Ad = request.Ad,
            Aciklama = request.Aciklama,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.AddAsync(dto));
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<ActionResult<RestoranDto>> Update(int id, [FromBody] UpdateRestoranRequest request, CancellationToken cancellationToken)
    {
        var dto = new RestoranDto
        {
            Id = id,
            TesisId = request.TesisId,
            IsletmeAlaniId = request.IsletmeAlaniId,
            YoneticiUserIds = request.YoneticiUserIds,
            GarsonUserIds = request.GarsonUserIds,
            Ad = request.Ad,
            Aciklama = request.Aciklama,
            AktifMi = request.AktifMi
        };

        return Ok(await _service.UpdateAsync(dto));
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("{restoranId:int}/yonetici-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateRestoranYoneticisiUser(
        int restoranId,
        [FromBody] UserDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.CreateRestoranYoneticisiUserAsync(restoranId, dto, cancellationToken));

    [HttpPost("{restoranId:int}/garson-kullanici")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> CreateRestoranGarsonuUser(
        int restoranId,
        [FromBody] UserDto dto,
        CancellationToken cancellationToken)
        => Ok(await _service.CreateRestoranGarsonuUserAsync(restoranId, dto, cancellationToken));
}

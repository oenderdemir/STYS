using Microsoft.AspNetCore.Mvc;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Users.DTO;

namespace STYS.Restoranlar.Controllers;

[Route("api/restoranlar")]
[ApiController]
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
        => Ok(await _service.GetListAsync(tesisId, cancellationToken));

    [HttpGet("isletme-alanlari")]
    [Permission(StructurePermissions.RestoranYonetimi.View)]
    public async Task<ActionResult<List<RestoranIsletmeAlaniSecenekDto>>> GetIsletmeAlaniSecenekleri([FromQuery] int tesisId, CancellationToken cancellationToken)
        => Ok(await _service.GetIsletmeAlaniSecenekleriAsync(tesisId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.View)]
    public async Task<ActionResult<RestoranDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<ActionResult<RestoranDto>> Create([FromBody] CreateRestoranRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<ActionResult<RestoranDto>> Update(int id, [FromBody] UpdateRestoranRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.RestoranYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
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

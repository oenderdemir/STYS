using Microsoft.AspNetCore.Mvc;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;

namespace STYS.Kurumlar.Controllers;

public class KurumController : UIController
{
    private readonly IKurumService _kurumService;

    public KurumController(IKurumService kurumService)
    {
        _kurumService = kurumService;
    }

    // TODO Tenant Faz 2/3: Kurum olusturma/guncelleme/silme sadece SuperAdmin yetkisine baglanacak.

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View)]
    public Task<List<KurumDto>> GetAll(CancellationToken cancellationToken)
    {
        return _kurumService.GetAllAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.View)]
    public async Task<ActionResult<KurumDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var kurum = await _kurumService.GetByIdAsync(id, cancellationToken);
        if (kurum is null)
        {
            return NotFound();
        }

        return Ok(kurum);
    }

    [HttpPost]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<KurumDto>> Create([FromBody] CreateKurumRequest request, CancellationToken cancellationToken)
    {
        var created = await _kurumService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<KurumDto>> Update(int id, [FromBody] UpdateKurumRequest request, CancellationToken cancellationToken)
    {
        var updated = await _kurumService.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _kurumService.DeleteAsync(id, cancellationToken);
        return Ok();
    }
}

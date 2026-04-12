using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.MusteriMenu.Dtos;
using STYS.MusteriMenu.Services;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.MusteriMenu.Controllers;

[Route("api/musteri-menu")]
[ApiController]
public class MusteriMenuController : UIController
{
    private readonly IMusteriMenuService _service;

    public MusteriMenuController(IMusteriMenuService service)
    {
        _service = service;
    }

    [HttpGet("{restoranId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MusteriMenuDto>> GetByRestoranId(int restoranId, CancellationToken cancellationToken)
        => Ok(await _service.GetByRestoranIdAsync(restoranId, cancellationToken));
}

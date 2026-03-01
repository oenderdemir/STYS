using Microsoft.AspNetCore.Mvc;
using STYS.YoneticiAdaylari.Dto;
using STYS.YoneticiAdaylari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.YoneticiAdaylari.Controllers;

public class YoneticiAdayController : UIController
{
    private readonly IYoneticiAdayService _yoneticiAdayService;

    public YoneticiAdayController(IYoneticiAdayService yoneticiAdayService)
    {
        _yoneticiAdayService = yoneticiAdayService;
    }

    [HttpGet]
    [Permission(StructurePermissions.TesisYonetimi.Manage, StructurePermissions.BinaYonetimi.Manage)]
    public async Task<ActionResult<List<YoneticiAdayDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _yoneticiAdayService.GetAllAsync(cancellationToken);
        return Ok(result);
    }
}

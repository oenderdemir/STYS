using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.KdvRaporlari.Dtos;
using STYS.Muhasebe.KdvRaporlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.KdvRaporlari.Controllers;

[Route("ui/muhasebe/kdv-raporlari")]
public class KdvRaporlariController : UIController
{
    private readonly IKdvRaporService _service;

    public KdvRaporlariController(IKdvRaporService service)
    {
        _service = service;
    }

    [HttpGet("ozet")]
    [Permission(
        StructurePermissions.MuhasebeKdvHareketRaporuYonetimi.View,
        StructurePermissions.MuhasebeKdvOzetRaporuYonetimi.View)]
    public async Task<IActionResult> GetOzet([FromQuery] KdvRaporFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetOzetAsync(filter, cancellationToken));

    [HttpGet("hareketler")]
    [Permission(
        StructurePermissions.MuhasebeKdvHareketRaporuYonetimi.View,
        StructurePermissions.MuhasebeKdvOzetRaporuYonetimi.View)]
    public async Task<IActionResult> GetHareketler([FromQuery] KdvRaporFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetHareketlerAsync(filter, cancellationToken));

    [HttpGet("tevkifat-ozet")]
    [Permission(
        StructurePermissions.MuhasebeKdvHareketRaporuYonetimi.View,
        StructurePermissions.MuhasebeKdvOzetRaporuYonetimi.View)]
    public async Task<IActionResult> GetTevkifatOzet([FromQuery] KdvRaporFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetTevkifatOzetAsync(filter, cancellationToken));

    [HttpGet("tevkifat-hareketler")]
    [Permission(
        StructurePermissions.MuhasebeKdvHareketRaporuYonetimi.View,
        StructurePermissions.MuhasebeKdvOzetRaporuYonetimi.View)]
    public async Task<IActionResult> GetTevkifatHareketler([FromQuery] KdvRaporFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetTevkifatHareketlerAsync(filter, cancellationToken));
}

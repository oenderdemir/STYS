using Microsoft.AspNetCore.Mvc;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.KantinYonetimi.KantinSatislari.Controllers;

[Route("ui/kantin-satis-iade")]
public class KantinSatisIadeController : UIController
{
    private readonly IKantinSatisIadeService _service;

    public KantinSatisIadeController(IKantinSatisIadeService service)
    {
        _service = service;
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KantinSatisIadeYonetimi.View)]
    public async Task<ActionResult<KantinSatisIadeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("ozet")]
    [Permission(StructurePermissions.KantinSatisIadeYonetimi.View)]
    public async Task<ActionResult<List<KantinSatisIadeOzetDto>>> GetOzet([FromQuery] int kantinSatisId, CancellationToken cancellationToken)
        => Ok(await _service.GetSatisIadeOzetiAsync(kantinSatisId, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.KantinSatisIadeYonetimi.Create)]
    public async Task<ActionResult<KantinSatisIadeDto>> Create([FromBody] CreateKantinSatisIadeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CreateAsync(request, cancellationToken));

    [HttpPost("{id:int}/kesinlestir")]
    [Permission(StructurePermissions.KantinSatisIadeYonetimi.Finalize)]
    public async Task<ActionResult<KantinSatisIadeDto>> Kesinlestir(int id, CancellationToken cancellationToken)
        => Ok(await _service.KesinlestirAsync(id, cancellationToken));
}

using Microsoft.AspNetCore.Mvc;
using STYS;
using STYS.ErisimTeshis.Dto;
using STYS.ErisimTeshis.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.ErisimTeshis.Controllers;

public class ErisimTeshisController : UIController
{
    private readonly IErisimTeshisService _service;

    public ErisimTeshisController(IErisimTeshisService service)
    {
        _service = service;
    }

    [HttpGet("referanslar")]
    [Permission(StructurePermissions.ErisimTeshisYonetimi.View)]
    public Task<ErisimTeshisReferansDto> GetReferanslar(CancellationToken cancellationToken)
    {
        return _service.GetReferanslarAsync(cancellationToken);
    }

    [HttpPost("teshis-et")]
    [Permission(StructurePermissions.ErisimTeshisYonetimi.View)]
    public Task<ErisimTeshisSonucDto> TeshisEt([FromBody] ErisimTeshisIstekDto request, CancellationToken cancellationToken)
    {
        return _service.TeshisEtAsync(request, cancellationToken);
    }
}

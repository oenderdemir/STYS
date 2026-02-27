using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOD.Platform.AspNetCore.Authorization;

namespace TOD.Platform.AspNetCore.Controllers;

[Authorize(Policy = TodPlatformAuthorizationConstants.ServicePolicy)]
[Route("service/[controller]")]
[ApiController]
public abstract class ServiceController : ControllerBase
{
}

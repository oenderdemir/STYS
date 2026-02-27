using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOD.Platform.AspNetCore.Authorization;

namespace TOD.Platform.AspNetCore.Controllers;

[Authorize(Policy = TodPlatformAuthorizationConstants.UiPolicy)]
[Route("ui/[controller]")]
[ApiController]
public abstract class UIController : ControllerBase
{
}

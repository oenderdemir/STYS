using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TOD.Platform.AspNetCore.Controllers;

[Authorize(Policy = "ServicePolicy")]
[Route("service/[controller]")]
[ApiController]
public abstract class ServiceController : ControllerBase
{
}

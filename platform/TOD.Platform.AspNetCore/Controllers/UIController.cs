using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TOD.Platform.AspNetCore.Controllers;

[Authorize(Policy = "UIPolicy")]
[Route("ui/[controller]")]
[ApiController]
public abstract class UIController : ControllerBase
{
}

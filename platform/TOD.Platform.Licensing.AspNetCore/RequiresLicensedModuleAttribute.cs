namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// Controller veya action'a uygulanarak belirli bir modül lisansı gerektirir.
/// LicensedModuleFilter tarafından okunur.
///
/// Kullanım:
///   [RequiresLicensedModule("Muhasebe")]
///   public class MuhasebeController : ControllerBase { }
///
///   [RequiresLicensedModule("Kamp")]
///   [HttpGet("kamplar")]
///   public IActionResult GetKamplar() { }
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresLicensedModuleAttribute : Attribute
{
    public string ModuleCode { get; }

    public RequiresLicensedModuleAttribute(string moduleCode)
    {
        ModuleCode = moduleCode;
    }
}

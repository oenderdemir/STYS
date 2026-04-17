using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// RequiresLicensedModule attribute'u olan endpoint'lerde modül lisansını kontrol eder.
/// Global filter olarak eklenir. Attribute yoksa geçer.
/// </summary>
public sealed class LicensedModuleFilter : IAsyncActionFilter
{
    private readonly ILicenseService _licenseService;
    private readonly ILogger<LicensedModuleFilter> _logger;

    public LicensedModuleFilter(
        ILicenseService licenseService,
        ILogger<LicensedModuleFilter> logger)
    {
        _licenseService = licenseService;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Önce action'da, sonra controller'da attribute ara
        var attribute = context.ActionDescriptor.EndpointMetadata
            .OfType<RequiresLicensedModuleAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            await next();
            return;
        }

        var isLicensed = await _licenseService.IsModuleLicensedAsync(
            attribute.ModuleCode, context.HttpContext.RequestAborted);

        if (!isLicensed)
        {
            _logger.LogWarning("Lisanssız modül erişimi engellendi: {Module}, Path: {Path}",
                attribute.ModuleCode, context.HttpContext.Request.Path);

            var response = new
            {
                error = "MODULE_NOT_LICENSED",
                message = $"'{attribute.ModuleCode}' modülü bu lisansla kullanılamaz.",
                moduleCode = attribute.ModuleCode
            };

            context.Result = new JsonResult(response)
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}

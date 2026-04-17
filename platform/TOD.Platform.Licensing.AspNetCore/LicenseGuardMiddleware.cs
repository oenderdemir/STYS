using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// Her HTTP isteğinde lisans durumunu kontrol eden middleware.
/// Lisans geçersizse veya süresi dolmuşsa 403 Forbidden döner.
/// ExcludedPaths ile belirli endpoint'ler (health, license-status) muaf tutulabilir.
/// </summary>
public sealed class LicenseGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LicenseGuardMiddleware> _logger;
    private readonly LicensingOptions _options;

    public LicenseGuardMiddleware(
        RequestDelegate next,
        IOptions<LicensingOptions> options,
        ILogger<LicenseGuardMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, ILicenseService licenseService)
    {
        var path = context.Request.Path.Value ?? "";

        // Muaf path kontrolü
        if (_options.ExcludedPaths.Any(excluded =>
                path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var status = await licenseService.GetCurrentStatusAsync(context.RequestAborted);

        if (!status.IsValid)
        {
            _logger.LogWarning("Lisanssız istek engellendi: {Path}. Hatalar: {Errors}",
                path, string.Join("; ", status.Errors));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "LICENSE_INVALID",
                message = "Geçerli bir lisans bulunamadı. Lütfen sistem yöneticinizle iletişime geçin.",
                details = status.Errors
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response),
                context.RequestAborted);
            return;
        }

        await _next(context);
    }
}

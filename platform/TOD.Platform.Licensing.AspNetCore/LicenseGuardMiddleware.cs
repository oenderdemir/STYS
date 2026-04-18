using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// Her HTTP isteginde lisans durumunu kontrol eden middleware.
/// Lisans gecersizse veya suresi dolmussa 403 Forbidden doner.
///
    /// ExcludedPaths eslesmesi guvenlik icin daraltilmistir:
    ///  - Varsayilan davranis exact-match'tir.
    ///  - Prefix esleme yalnizca "/*" ile biterse aktif olur (segment-aware).
    /// Ornek:
    ///  - "/api/license/status" -> sadece exact
    ///  - "/auth/*" -> "/auth" ve "/auth/..." eslenir
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
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcluded(path))
        {
            await _next(context);
            return;
        }

        var status = await licenseService.GetCurrentStatusAsync(context.RequestAborted);

        if (!status.IsValid)
        {
            _logger.LogWarning("Lisanssiz istek engellendi: {Path}. Hatalar: {Errors}",
                path, string.Join("; ", status.Errors));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = "LICENSE_INVALID",
                message = "Gecerli bir lisans bulunamadi. Lutfen sistem yoneticinizle iletisime geciniz.",
                details = status.Errors
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response),
                context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private bool IsExcluded(string path)
    {
        if (_options.ExcludedPaths.Count == 0)
            return false;

        foreach (var excluded in _options.ExcludedPaths)
        {
            if (string.IsNullOrWhiteSpace(excluded))
                continue;

            if (IsExcludedPathMatch(path, excluded))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Excluded path match:
    /// - exact (varsayilan)
    /// - "/*" ile segment-aware prefix
    /// </summary>
    private static bool IsExcludedPathMatch(string path, string configured)
    {
        var normalizedPath = path.TrimEnd('/');
        var normalizedConfigured = configured.TrimEnd('/');

        if (normalizedConfigured.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = normalizedConfigured[..^2].TrimEnd('/');
            return IsSegmentPrefixMatch(normalizedPath, prefix);
        }

        return string.Equals(normalizedPath, normalizedConfigured, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSegmentPrefixMatch(string path, string prefix)
    {
        if (string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return path.Length > prefix.Length && path[prefix.Length] == '/';
    }
}

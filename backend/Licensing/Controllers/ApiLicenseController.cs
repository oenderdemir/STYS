using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using STYS.Licensing.Dto;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Licensing.Abstractions;

namespace STYS.Licensing.Controllers;

[Route("api/license")]
[ApiController]
public class ApiLicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseSignatureVerifier _signatureVerifier;
    private readonly LicensingOptions _options;
    private readonly ILogger<ApiLicenseController> _logger;

    public ApiLicenseController(
        ILicenseService licenseService,
        ILicenseSignatureVerifier signatureVerifier,
        IOptions<LicensingOptions> options,
        ILogger<ApiLicenseController> logger)
    {
        _licenseService = licenseService;
        _signatureVerifier = signatureVerifier;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("status")]
    [Permission(StructurePermissions.LisansYonetimi.View)]
    public async Task<ActionResult<LicenseStatusDto>> GetStatus(CancellationToken ct)
    {
        var result = await _licenseService.GetCurrentStatusAsync(ct);
        return Ok(ToDto(result));
    }

    [HttpPost("upload")]
    [Permission(StructurePermissions.LisansYonetimi.Manage)]
    public async Task<ActionResult<LicenseStatusDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Gecerli bir lisans dosyasi secin.");

        if (file.Length > 64 * 1024)
            return BadRequest("Lisans dosyasi cok buyuk.");

        LicenseDocument document;
        try
        {
            await using var stream = file.OpenReadStream();
            document = await JsonSerializer.DeserializeAsync<LicenseDocument>(stream, cancellationToken: ct)
                       ?? throw new JsonException("null");
        }
        catch (JsonException)
        {
            return BadRequest("Gecersiz lisans dosyasi formati.");
        }

        if (!_signatureVerifier.Verify(document))
        {
            _logger.LogWarning("Gecersiz imzali lisans yukleme denemesi (api/license/upload).");
            return BadRequest("Lisans imzasi gecersiz. Dosya degistirilmis olabilir.");
        }

        var targetPath = _options.LicenseFilePath;
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(targetPath, json, ct);

        _licenseService.InvalidateCache();

        _logger.LogInformation(
            "Yeni lisans yuklendi (api/license/upload). LicenseId: {LicenseId}, Customer: {Customer}",
            document.LicenseId,
            document.CustomerCode);

        var result = await _licenseService.GetCurrentStatusAsync(ct);
        return Ok(ToDto(result));
    }

    private static LicenseStatusDto ToDto(LicenseValidationResult result)
    {
        return new LicenseStatusDto
        {
            IsValid = result.IsValid,
            LicenseId = result.License?.LicenseId,
            ProductCode = result.License?.ProductCode,
            CustomerCode = result.License?.CustomerCode,
            CustomerName = result.License?.CustomerName,
            EnvironmentName = result.License?.EnvironmentName,
            InstanceId = result.License?.InstanceId,
            IssuedAtUtc = result.License?.IssuedAtUtc,
            ExpiresAtUtc = result.License?.ExpiresAtUtc,
            EnabledModules = result.License?.EnabledModules ?? [],
            Errors = result.Errors.ToList()
        };
    }
}

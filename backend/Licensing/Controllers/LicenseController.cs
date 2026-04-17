using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using STYS.Licensing.Dto;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.Abstractions;

namespace STYS.Licensing.Controllers;

public class LicenseController : UIController
{
    private readonly ILicenseService _licenseService;
    private readonly ILicenseSignatureVerifier _signatureVerifier;
    private readonly LicensingOptions _options;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(
        ILicenseService licenseService,
        ILicenseSignatureVerifier signatureVerifier,
        IOptions<LicensingOptions> options,
        ILogger<LicenseController> logger)
    {
        _licenseService = licenseService;
        _signatureVerifier = signatureVerifier;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Mevcut lisans durumunu doner.</summary>
    [HttpGet("status")]
    [Permission(StructurePermissions.LisansYonetimi.View)]
    public async Task<ActionResult<LicenseStatusDto>> GetStatus(CancellationToken ct)
    {
        var result = await _licenseService.GetCurrentStatusAsync(ct);
        return Ok(ToDto(result));
    }

    /// <summary>Lisans dosyasi yukler ve dogrular.</summary>
    [HttpPost("upload")]
    [Permission(StructurePermissions.LisansYonetimi.Manage)]
    public async Task<ActionResult<LicenseStatusDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Gecerli bir lisans dosyasi secin.");

        if (file.Length > 64 * 1024) // 64 KB limit
            return BadRequest("Lisans dosyasi cok buyuk.");

        // Oku ve parse et
        LicenseDocument document;
        try
        {
            using var stream = file.OpenReadStream();
            document = await JsonSerializer.DeserializeAsync<LicenseDocument>(stream, cancellationToken: ct)
                       ?? throw new JsonException("null");
        }
        catch (JsonException)
        {
            return BadRequest("Gecersiz lisans dosyasi formati.");
        }

        // Imza kontrolu — yuklemeden once
        if (!_signatureVerifier.Verify(document))
        {
            _logger.LogWarning("Gecersiz imzali lisans yukleme denemesi.");
            return BadRequest("Lisans imzasi gecersiz. Dosya degistirilmis olabilir.");
        }

        // Dosyayi kaydet
        var targetPath = _options.LicenseFilePath;
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(targetPath, json, ct);

        // Cache'i temizle ki bir sonraki istek yeni lisansi kullansin
        _licenseService.InvalidateCache();

        _logger.LogInformation("Yeni lisans yuklendi. LicenseId: {LicenseId}, Customer: {Customer}",
            document.LicenseId, document.CustomerCode);

        // Yeniden dogrula
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

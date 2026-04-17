using System.Text.Json;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Lisans dosyasını diskten okur ve LicenseDocument'a deserialize eder.
/// </summary>
public sealed class FileLicenseReader : ILicenseReader
{
    private readonly LicensingOptions _options;

    public FileLicenseReader(IOptions<LicensingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<LicenseDocument> ReadAsync(CancellationToken cancellationToken = default)
    {
        var path = _options.LicenseFilePath;

        if (!File.Exists(path))
            throw new LicenseException($"Lisans dosyası bulunamadı: {path}");

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<LicenseDocument>(stream, cancellationToken: cancellationToken);

            if (document is null)
                throw new LicenseException("Lisans dosyası boş veya geçersiz JSON formatında.");

            return document;
        }
        catch (JsonException ex)
        {
            throw new LicenseException("Lisans dosyası parse edilemedi.", ex);
        }
    }
}

using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosGunSonuSlipStorage
{
    /// <summary>Atomically writes a gün sonu slip image and returns its root-relative path
    /// (<c>&lt;kurumId&gt;/&lt;posCihaziId&gt;/&lt;posGunSonuIslemiId&gt;/&lt;file&gt;</c>).</summary>
    Task<string> StoreAsync(int kurumId, int posCihaziId, int posGunSonuIslemiId, string fileName, byte[] content, CancellationToken cancellationToken);

    Stream OpenRead(string relativePath);
    void Delete(string? relativePath);
}

/// <summary>
/// Filesystem gün sonu slip storage laid out as
/// <c>&lt;root&gt;/&lt;kurumId&gt;/&lt;posCihaziId&gt;/&lt;posGunSonuIslemiId&gt;/&lt;file&gt;</c>.
/// Writes go through a random temp file + atomic move; paths are derived only from trusted server-side
/// IDs and the content hash, never from client/PAVO-supplied filenames.
/// </summary>
public sealed class PosGunSonuSlipStorage : IPosGunSonuSlipStorage
{
    private readonly PosGunSonuSlipStorageOptions _options;

    public PosGunSonuSlipStorage(IOptions<PosGunSonuSlipStorageOptions> options)
    {
        _options = options.Value ?? new PosGunSonuSlipStorageOptions();
    }

    public async Task<string> StoreAsync(int kurumId, int posCihaziId, int posGunSonuIslemiId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var directory = Path.Combine(
            root,
            kurumId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            posCihaziId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            posGunSonuIslemiId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Directory.CreateDirectory(directory);

        var finalPath = Path.Combine(directory, SanitizeSegment(fileName));
        EnsureUnderRoot(root, finalPath);

        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.WriteAsync(content, cancellationToken);
                await file.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        return Path.GetRelativePath(root, finalPath);
    }

    public Stream OpenRead(string relativePath)
    {
        var root = ResolveRoot();
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        EnsureUnderRoot(root, fullPath);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            var root = ResolveRoot();
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
            EnsureUnderRoot(root, fullPath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
        }
    }

    private string ResolveRoot()
    {
        var configured = _options.RootPath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new BaseException("POS gün sonu slip depolama yolu yapılandırılmamış.", 500);
        }

        var root = Path.GetFullPath(configured);
        Directory.CreateDirectory(root);
        return root;
    }

    private static void EnsureUnderRoot(string root, string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(fullPath).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Gün sonu slip dosya yolu geçersiz.", 400);
        }
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var safe = new string([.. trimmed.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')]);

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            throw new BaseException("Gün sonu slip dosya adı geçersiz.", 400);
        }

        return safe;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

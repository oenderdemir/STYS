using Microsoft.Extensions.Options;
using STYS.Entegrasyonlar.Pos.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosReceiptStorage
{
    /// <summary>
    /// Atomically writes a receipt image and returns its root-relative path (e.g.
    /// <c>&lt;kurumId&gt;/&lt;posOdemeIslemiId&gt;/customer.png</c>). The path is derived only from
    /// trusted server-side IDs and the fixed tip enum; client/Pavo-supplied names are never used.
    /// </summary>
    Task<string> StoreAsync(int kurumId, int posOdemeIslemiId, string fileName, byte[] content, CancellationToken cancellationToken);

    /// <summary>Opens a previously stored receipt for reading. The relative path must be one produced
    /// by <see cref="StoreAsync"/>; it is resolved strictly under the configured root.</summary>
    Stream OpenRead(string relativePath);

    /// <summary>Best-effort delete of a previously stored receipt.</summary>
    void Delete(string? relativePath);
}

/// <summary>
/// Filesystem receipt storage laid out as
/// <c>&lt;root&gt;/&lt;kurumId&gt;/&lt;posOdemeIslemiId&gt;/&lt;tip&gt;.png</c>. Writes go through a
/// random temp file + atomic move so a partial/corrupt PNG is never left at the final path.
/// </summary>
public sealed class PosReceiptStorage : IPosReceiptStorage
{
    private readonly PosReceiptStorageOptions _options;

    public PosReceiptStorage(IOptions<PosReceiptStorageOptions> options)
    {
        _options = options.Value ?? new PosReceiptStorageOptions();
    }

    public async Task<string> StoreAsync(int kurumId, int posOdemeIslemiId, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var directory = Path.Combine(
            root,
            kurumId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            posOdemeIslemiId.ToString(System.Globalization.CultureInfo.InvariantCulture));

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
            // Cleanup is best effort: a leftover file must never mask the primary operation.
        }
    }

    private string ResolveRoot()
    {
        var configured = _options.RootPath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new BaseException("POS receipt depolama yolu yapılandırılmamış.", 500);
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
            throw new BaseException("Receipt dosya yolu geçersiz.", 400);
        }
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var safe = new string([.. trimmed.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')]);

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            throw new BaseException("Receipt dosya adı geçersiz.", 400);
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

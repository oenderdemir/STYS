using Microsoft.Extensions.Options;
using STYS.Agent.Options;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Services;

public interface IAgentReleasePackageStorage
{
    Task<TempPackage> WriteTempAsync(Stream content, long maxBytes, CancellationToken cancellationToken);
    string MoveToFinal(TempPackage temp, int kurumId, int releaseId, string version, string runtimeIdentifier);
    void TryDelete(string? path);
}

/// <summary>A package staged outside its final location until the release row exists.</summary>
public sealed record TempPackage(string Path, long Length, string Sha256);

/// <summary>
/// Stores release packages under a configured root, laid out as
/// <c>&lt;root&gt;/&lt;kurumId&gt;/&lt;releaseId&gt;/&lt;rid&gt;/stys-agent-&lt;version&gt;-&lt;rid&gt;.zip</c>.
/// Every path segment is derived from validated server-side values; the client-supplied file name
/// is never used to build a path.
/// </summary>
public sealed class AgentReleasePackageStorage : IAgentReleasePackageStorage
{
    private readonly AgentReleasePublishingOptions _options;

    public AgentReleasePackageStorage(IOptions<AgentReleasePublishingOptions>? options = null)
    {
        _options = options?.Value ?? new AgentReleasePublishingOptions();
    }

    public async Task<TempPackage> WriteTempAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var root = ResolveRoot();
        var tempDirectory = Path.Combine(root, ".incoming");
        Directory.CreateDirectory(tempDirectory);

        var tempPath = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.tmp");

        try
        {
            long written;
            string hash;

            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var buffer = new byte[81920];
                int read;
                written = 0;

                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    if (written > maxBytes)
                    {
                        throw new BaseException($"Release paketi izin verilen boyutu aşıyor ({maxBytes} bayt).", 400);
                    }

                    sha.TransformBlock(buffer, 0, read, null, 0);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                sha.TransformFinalBlock([], 0, 0);
                hash = Convert.ToHexString(sha.Hash!);
            }

            if (written == 0)
            {
                throw new BaseException("Release paketi boş.", 400);
            }

            return new TempPackage(tempPath, written, hash);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public string MoveToFinal(TempPackage temp, int kurumId, int releaseId, string version, string runtimeIdentifier)
    {
        var root = ResolveRoot();
        var directory = Path.Combine(
            root,
            kurumId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            releaseId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SanitizeSegment(runtimeIdentifier));

        Directory.CreateDirectory(directory);

        var finalPath = Path.Combine(directory, $"stys-agent-{SanitizeSegment(version)}-{SanitizeSegment(runtimeIdentifier)}.zip");

        // Guard against a crafted version/RID escaping the storage root even after sanitisation.
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(finalPath).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BaseException("Release paketi hedef yolu geçersiz.", 400);
        }

        File.Move(temp.Path, finalPath, overwrite: false);
        return finalPath;
    }

    public void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort: a leftover temp file must never mask the original failure.
        }
    }

    private string ResolveRoot()
    {
        var configured = _options.StorageRootPath?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new BaseException("Agent release paket depolama yolu yapılandırılmamış.", 500);
        }

        var root = Path.GetFullPath(configured);
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Reduces a value to characters that are safe in a single path segment.</summary>
    private static string SanitizeSegment(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        var safe = new string([.. trimmed.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')]);

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            throw new BaseException("Release paketi yol bileşeni geçersiz.", 400);
        }

        return safe;
    }
}

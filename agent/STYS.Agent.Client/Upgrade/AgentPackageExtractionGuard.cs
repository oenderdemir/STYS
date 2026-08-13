using System.IO.Compression;

namespace STYS.Agent.Client.Upgrade;

public static class AgentPackageExtractionGuard
{
    public static void ExtractPackage(string packagePath, string extractDirectory)
    {
        if (Directory.Exists(extractDirectory))
        {
            Directory.Delete(extractDirectory, true);
        }

        Directory.CreateDirectory(extractDirectory);
        var root = Path.GetFullPath(extractDirectory);

        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries)
        {
            ValidateEntry(entry, root);

            var destinationPath = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!IsPathWithinRoot(root, destinationPath))
            {
                throw new InvalidOperationException($"Güvensiz arşiv yolu: {entry.FullName}");
            }

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                EnsureDirectorySafe(destinationPath, root);
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var parentDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                EnsureDirectorySafe(parentDirectory, root);
                Directory.CreateDirectory(parentDirectory);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static void ValidateEntry(ZipArchiveEntry entry, string root)
    {
        if (Path.IsPathRooted(entry.FullName))
        {
            throw new InvalidOperationException($"Kök yol içeren arşiv girişine izin verilmez: {entry.FullName}");
        }

        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixType == 0xA000)
        {
            throw new InvalidOperationException($"Symlink içeren arşiv girişine izin verilmez: {entry.FullName}");
        }

        var normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Contains(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || normalized == ".." || normalized.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Güvensiz arşiv yolu: {entry.FullName}");
        }

        if (normalized.Contains(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Güvensiz arşiv yolu: {entry.FullName}");
        }
    }

    private static bool IsPathWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void EnsureDirectorySafe(string path, string root)
    {
        var current = Path.GetFullPath(path);
        while (!string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"Arşiv dışı escape riski taşıyan dizin bulundu: {path}");
                }
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent))
            {
                break;
            }

            current = parent;
        }
    }
}

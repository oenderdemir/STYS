using System.IO.Compression;
using Serilog.Sinks.File.Archive;

namespace TOD.Platform.AspNetCore.Logging;

public static class SerilogHooks
{
    public const string DefaultArchiveDirectoryFormat = "Logs/archive/{UtcDate:yyyy}/{UtcDate:MM}";

    private static string _archiveDirectoryFormat = DefaultArchiveDirectoryFormat;
    private static ArchiveHooks _archiveHooks = CreateHooks(DefaultArchiveDirectoryFormat);

    public static ArchiveHooks ArchiveHooks => _archiveHooks;

    public static void Configure(string? archiveDirectoryFormat)
    {
        _archiveDirectoryFormat = string.IsNullOrWhiteSpace(archiveDirectoryFormat)
            ? DefaultArchiveDirectoryFormat
            : archiveDirectoryFormat.Trim();

        _archiveHooks = CreateHooks(_archiveDirectoryFormat);
    }

    private static ArchiveHooks CreateHooks(string archiveDirectoryFormat) =>
        new(CompressionLevel.Fastest, archiveDirectoryFormat);
}

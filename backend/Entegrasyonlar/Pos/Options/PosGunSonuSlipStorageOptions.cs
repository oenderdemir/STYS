namespace STYS.Entegrasyonlar.Pos.Options;

public sealed class PosGunSonuSlipStorageOptions
{
    public const string SectionName = "PosGunSonuSlipStorage";

    /// <summary>Root directory for gün sonu slip images, outside wwwroot (never statically served).</summary>
    public string? RootPath { get; set; }

    /// <summary>Maximum decoded slip image size in bytes (default 10 MB).</summary>
    public long MaxImageBytes { get; set; } = 10L * 1024 * 1024;
}

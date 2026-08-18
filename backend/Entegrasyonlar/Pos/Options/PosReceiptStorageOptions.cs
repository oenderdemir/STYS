namespace STYS.Entegrasyonlar.Pos.Options;

public sealed class PosReceiptStorageOptions
{
    public const string SectionName = "PosReceiptStorage";

    /// <summary>
    /// Root directory that holds payment receipt slip images. Kept outside wwwroot so receipts are
    /// never statically servable; access goes through the authenticated POS receipt endpoint only.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>Maximum decoded receipt image size in bytes (default 5 MB per slip).</summary>
    public long MaxImageBytes { get; set; } = 5L * 1024 * 1024;
}

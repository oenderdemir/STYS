namespace STYS.Kurumlar.Options;

public class KurumLogoStorageOptions
{
    public const string SectionName = "KurumLogoStorage";

    public string RootPath { get; set; } = "Uploads/KurumLogolari";

    public long MaxFileSizeBytes { get; set; } = 1048576;

    public List<string> AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "image/webp"
    ];
}

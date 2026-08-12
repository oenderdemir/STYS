namespace STYS.Entegrasyonlar.Pos.Options;

public sealed class PosOperationalHealthOptions
{
    public const string SectionName = "PosOperationalHealth";

    public int FreshnessThresholdMinutes { get; set; } = 5;
    public int CommandTimeoutMinutes { get; set; } = 2;

    public TimeSpan FreshnessThreshold => TimeSpan.FromMinutes(Math.Max(1, FreshnessThresholdMinutes));
    public TimeSpan CommandTimeout => TimeSpan.FromMinutes(Math.Max(1, CommandTimeoutMinutes));
}

namespace STYS.Entegrasyonlar.Pos.Options;

public sealed class PosOdemeTakipOptions
{
    public const string SectionName = "PosOdemeTakip";

    public bool Enabled { get; set; } = true;
    public int WorkerIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 120;
    public int TimeoutMinutes { get; set; } = 30;
    public int BaseRetrySeconds { get; set; } = 3;
    public int MaxRetrySeconds { get; set; } = 30;
}

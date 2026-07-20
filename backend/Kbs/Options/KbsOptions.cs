namespace STYS.Kbs.Options;

public class KbsOptions
{
    public const string SectionName = "Kbs";
    public bool LiveConnectorsEnabled { get; set; }
    public int WorkerIntervalSeconds { get; set; } = 15;
    public int MaxAttempts { get; set; } = 5;
    public int SendingRecoveryMinutes { get; set; } = 10;
    public string? EgmTemplatePath { get; set; }
    public Dictionary<string, int> EgmColumns { get; set; } = [];
    public string FakeResponse { get; set; } = "Basarili";
    public string? PayloadProtectionKeyRingPath { get; set; }
}

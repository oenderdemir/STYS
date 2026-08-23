namespace STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;

public static class StokMaliyetYontemleri
{
    public const string AgirlikliOrtalama = "AgirlikliOrtalama";
    public const string FIFO = "FIFO";
    public const string LIFO = "LIFO";

    public static readonly string[] All =
    [
        AgirlikliOrtalama,
        FIFO,
        LIFO
    ];
}

public class StokMaliyetPolitikasiDto
{
    public int Id { get; set; }
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public string MaliyetYontemi { get; set; } = string.Empty;
}

public class CurrentStokMaliyetPolitikasiDto
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public string? MaliyetYontemi { get; set; }
    public bool PolitikaSecildiMi { get; set; }
}

public class UpsertStokMaliyetPolitikasiRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public string MaliyetYontemi { get; set; } = string.Empty;
}

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

public class FifoBaslangicStoguSatirDto
{
    public int DepoId { get; set; }
    public string DepoKod { get; set; } = string.Empty;
    public string DepoAd { get; set; } = string.Empty;
    public int TasinirKartId { get; set; }
    public string StokKodu { get; set; } = string.Empty;
    public string TasinirKartAd { get; set; } = string.Empty;
    public string Birim { get; set; } = string.Empty;
    public decimal MevcutStokMiktari { get; set; }
    public decimal FifoKatmanMiktari { get; set; }
    public decimal KatmansizMiktar { get; set; }
    public decimal? OnerilenBirimMaliyet { get; set; }
    public bool MaliyetGuvenilirMi { get; set; }
}

public class CreateFifoBaslangicStoguRequest
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public List<CreateFifoBaslangicStoguSatirRequest> Satirlar { get; set; } = [];
}

public class CreateFifoBaslangicStoguSatirRequest
{
    public int DepoId { get; set; }
    public int TasinirKartId { get; set; }
    public decimal BirimMaliyet { get; set; }
}

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonCheckInKontrolDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public bool CheckInYapilabilir { get; set; }

    public List<RezervasyonCheckInUyariDto> Uyarilar { get; set; } = [];
}

public class RezervasyonCheckInUyariDto
{
    public int OdaId { get; set; }

    public string OdaNo { get; set; } = string.Empty;

    public string BinaAdi { get; set; } = string.Empty;

    public string TemizlikDurumu { get; set; } = string.Empty;

    public string Mesaj { get; set; } = string.Empty;

    public bool EngelleyiciMi { get; set; }
}


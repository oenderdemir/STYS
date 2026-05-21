namespace STYS.Muhasebe.Dashboard.Dtos;

public class MuhasebeDashboardFilterDto
{
    public int? TesisId { get; set; }
    public int? MaliYil { get; set; }
    public int? Donem { get; set; }
}

public class MuhasebeDashboardDto
{
    public int? TesisId { get; set; }
    public int MaliYil { get; set; }
    public int? Donem { get; set; }

    public int AcikDonemSayisi { get; set; }
    public int KapaliDonemSayisi { get; set; }

    public int TaslakFisSayisi { get; set; }
    public int OnayliFisSayisi { get; set; }
    public int IptalFisSayisi { get; set; }
    public int TersKayitFisSayisi { get; set; }

    public int DengesizTaslakFisSayisi { get; set; }

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Fark { get; set; }

    public List<MuhasebeDashboardDonemOzetDto> AcikDonemler { get; set; } = [];
    public List<MuhasebeDashboardFisOzetDto> SonFisler { get; set; } = [];
    public List<MuhasebeDashboardUyariDto> Uyarilar { get; set; } = [];
}

public class MuhasebeDashboardDonemOzetDto
{
    public int Id { get; set; }
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }
    public DateTime BaslangicTarihi { get; set; }
    public DateTime BitisTarihi { get; set; }
    public bool KapaliMi { get; set; }
}

public class MuhasebeDashboardFisOzetDto
{
    public int Id { get; set; }
    public int TesisId { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public int? YevmiyeNo { get; set; }
    public DateTime FisTarihi { get; set; }
    public int MaliYil { get; set; }
    public int Donem { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public string? Aciklama { get; set; }
}

public class MuhasebeDashboardUyariDto
{
    public string Tip { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string Severity { get; set; } = "warn";
}

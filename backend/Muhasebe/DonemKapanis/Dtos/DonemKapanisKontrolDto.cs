namespace STYS.Muhasebe.DonemKapanis.Dtos;

public class DonemKapanisKontrolFilterDto
{
    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }
}

public class DonemKapanisKontrolDto
{
    public int? DonemId { get; set; }

    public int TesisId { get; set; }
    public int MaliYil { get; set; }
    public int DonemNo { get; set; }

    public bool DonemVarMi { get; set; }
    public bool DonemKapaliMi { get; set; }
    public bool KapatilabilirMi { get; set; }

    public int TaslakFisSayisi { get; set; }
    public int DengesizTaslakFisSayisi { get; set; }
    public int OnayliFisSayisi { get; set; }
    public int IptalFisSayisi { get; set; }
    public int TersKayitFisSayisi { get; set; }

    public int YevmiyeNoEksikOnayliFisSayisi { get; set; }
    public int DengesizOnayliFisSayisi { get; set; }

    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public decimal Fark { get; set; }

    public List<DonemKapanisKontrolMaddeDto> Maddeler { get; set; } = new();
    public List<DonemKapanisKontrolFisOzetDto> ProblemliFisler { get; set; } = new();
}

public class DonemKapanisKontrolMaddeDto
{
    public string Kod { get; set; } = string.Empty;
    public string Baslik { get; set; } = string.Empty;
    public string Mesaj { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public bool BasariliMi { get; set; }
    public bool BloklayiciMi { get; set; }
    public string? Route { get; set; }
}

public class DonemKapanisKontrolFisOzetDto
{
    public int Id { get; set; }
    public string FisNo { get; set; } = string.Empty;
    public int? YevmiyeNo { get; set; }
    public DateTime FisTarihi { get; set; }
    public string FisTipi { get; set; } = string.Empty;
    public string Durum { get; set; } = string.Empty;
    public decimal ToplamBorc { get; set; }
    public decimal ToplamAlacak { get; set; }
    public string ProblemTipi { get; set; } = string.Empty;
}

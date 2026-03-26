namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisIslemSonucDto
{
    public string IslemAnahtari { get; set; } = string.Empty;

    public string IslemAdi { get; set; } = string.Empty;

    public string GerekliYetki { get; set; } = string.Empty;

    public bool YetkiVar { get; set; }

    public bool TesisScopeGerekli { get; set; }

    public bool? TesisScopeUygun { get; set; }

    public bool Sonuc { get; set; }

    public string Durum { get; set; } = string.Empty;

    public string EngelKodu { get; set; } = string.Empty;

    public string Aciklama { get; set; } = string.Empty;

    public string Oneri { get; set; } = string.Empty;
}

namespace STYS.ErisimTeshis.Dto;

public class ErisimTeshisSonucDto
{
    public ErisimTeshisKullaniciDto Kullanici { get; set; } = new();

    public ErisimTeshisModulDto Modul { get; set; } = new();

    public ErisimTeshisTesisDto? SeciliTesis { get; set; }

    public List<ErisimTeshisKullaniciGrupDto> KullaniciGruplari { get; set; } = [];

    public List<string> Yetkiler { get; set; } = [];

    public ErisimTeshisScopeDto Scope { get; set; } = new();

    public ErisimTeshisMenuGorunumDto MenuGorunumu { get; set; } = new();

    public List<ErisimTeshisIslemSonucDto> Islemler { get; set; } = [];

    public string GenelDurum { get; set; } = string.Empty;

    public int BasariliIslemSayisi { get; set; }

    public int UyariIslemSayisi { get; set; }

    public int EngelliIslemSayisi { get; set; }

    public List<string> EksikYetkiler { get; set; } = [];

    public List<string> OnerilenAksiyonlar { get; set; } = [];

    public string DestekNotu { get; set; } = string.Empty;

    public string Ozet { get; set; } = string.Empty;
}

namespace STYS.Kamp.Dto;

public class KampPuanKuraliYonetimBaglamDto
{
    public List<KampProgramiSecenekDto> Programlar { get; set; } = [];

    public List<KampPuanBasvuruSahibiTipSecenekDto> GlobalBasvuruSahibiTipleri { get; set; } = [];

    public List<KampPuanKuralSetiDto> KuralSetleri { get; set; } = [];

    public List<KampPuanBasvuruSahibiTipiDto> BasvuruSahibiTipleri { get; set; } = [];

    public List<KampSecenekDto> KatilimciTipleri { get; set; } = [];
}

public class KampPuanBasvuruSahibiTipSecenekDto
{
    public int Id { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;
}

public class KampPuanKuraliYonetimKaydetRequestDto
{
    public List<KampPuanKuralSetiDto> KuralSetleri { get; set; } = [];

    public List<KampPuanBasvuruSahibiTipiDto> BasvuruSahibiTipleri { get; set; } = [];
}

public class KampPuanKuralSetiDto
{
    public int? Id { get; set; }

    public int KampProgramiId { get; set; }

    public string? KampProgramiAd { get; set; }

    public int KampYili { get; set; }

    public int OncekiYilSayisi { get; set; }

    public int KatilimCezaPuani { get; set; }

    public int KatilimciBasinaPuan { get; set; } = 10;

    public bool AktifMi { get; set; }
}

public class KampPuanBasvuruSahibiTipiDto
{
    public int? Id { get; set; }

    public int KampProgramiId { get; set; }

    public int KampBasvuruSahibiTipiId { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public int OncelikSirasi { get; set; }

    public int TabanPuan { get; set; }

    public bool HizmetYiliPuaniAktifMi { get; set; }

    public int EmekliBonusPuani { get; set; }

    public string? VarsayilanKatilimciTipiKodu { get; set; }

    public bool AktifMi { get; set; }
}

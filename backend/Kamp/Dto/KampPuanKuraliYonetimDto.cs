namespace STYS.Kamp.Dto;

public class KampPuanKuraliYonetimBaglamDto
{
    public List<KampProgramiSecenekDto> Programlar { get; set; } = [];

    public List<KampPuanBasvuruSahibiTipSecenekDto> GlobalBasvuruSahibiTipleri { get; set; } = [];

    public List<KampProgramiParametreAyariDto> ProgramParametreAyarlari { get; set; } = [];

    public List<KampKonaklamaTarifeYonetimDto> KonaklamaTarifeleri { get; set; } = [];

    public List<KampPuanKuralSetiDto> KuralSetleri { get; set; } = [];

    public List<KampPuanBasvuruSahibiTipiDto> BasvuruSahibiTipleri { get; set; } = [];

    public List<KampSecenekDto> KatilimciTipleri { get; set; } = [];

    public KampYasUcretKuraliDto YasUcretKurali { get; set; } = new();
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

    public List<KampProgramiParametreAyariDto> ProgramParametreAyarlari { get; set; } = [];

    public List<KampKonaklamaTarifeYonetimDto> KonaklamaTarifeleri { get; set; } = [];

    public KampYasUcretKuraliDto YasUcretKurali { get; set; } = new();
}

public class KampProgramiParametreAyariDto
{
    public int? Id { get; set; }

    public int KampProgramiId { get; set; }

    public string? KampProgramiAd { get; set; }

    public decimal KamuAvansKisiBasi { get; set; } = 1700m;

    public decimal DigerAvansKisiBasi { get; set; } = 2550m;

    public int VazgecmeIadeGunSayisi { get; set; } = 7;

    public decimal GecBildirimGunlukKesintiyUzdesi { get; set; } = 0.05m;

    public int NoShowSuresiGun { get; set; } = 2;

    public bool AktifMi { get; set; } = true;
}

public class KampKonaklamaTarifeYonetimDto
{
    public int? Id { get; set; }

    public int KampProgramiId { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public int MinimumKisi { get; set; }

    public int MaksimumKisi { get; set; }

    public decimal KamuGunlukUcret { get; set; }

    public decimal DigerGunlukUcret { get; set; }

    public decimal BuzdolabiGunlukUcret { get; set; }

    public decimal TelevizyonGunlukUcret { get; set; }

    public decimal KlimaGunlukUcret { get; set; }

    public bool AktifMi { get; set; } = true;
}

public class KampYasUcretKuraliDto
{
    public int? Id { get; set; }

    public int UcretsizCocukMaxYas { get; set; } = 2;

    public int YarimUcretliCocukMaxYas { get; set; } = 6;

    public decimal YemekOrani { get; set; } = 0.50m;

    public bool AktifMi { get; set; } = true;
}

public class KampPuanKuralSetiDto
{
    public int? Id { get; set; }

    public int KampProgramiId { get; set; }

    public string? KampProgramiAd { get; set; }

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

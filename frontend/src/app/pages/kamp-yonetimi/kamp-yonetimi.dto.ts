export interface KampProgramiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aciklama?: string | null;
    aktifMi: boolean;
}

export interface KampProgramiSecenekDto {
    id: number;
    ad: string;
}

export interface KampPuanKuralSetiDto {
    id?: number | null;
    kampProgramiId: number;
    kampProgramiAd?: string | null;
    kampYili: number;
    oncekiYilSayisi: number;
    katilimCezaPuani: number;
    katilimciBasinaPuan: number;
    aktifMi: boolean;
}

export interface KampPuanBasvuruSahibiTipiDto {
    id?: number | null;
    kampProgramiId: number;
    kampBasvuruSahibiTipiId: number;
    kod: string;
    ad: string;
    oncelikSirasi: number;
    tabanPuan: number;
    hizmetYiliPuaniAktifMi: boolean;
    emekliBonusPuani: number;
    varsayilanKatilimciTipiKodu?: string | null;
    aktifMi: boolean;
}

export interface KampPuanBasvuruSahibiTipSecenekDto {
    id: number;
    kod: string;
    ad: string;
}

export interface KampProgramiParametreAyariDto {
    id?: number | null;
    kampProgramiId: number;
    kampProgramiAd?: string | null;
    kamuAvansKisiBasi: number;
    digerAvansKisiBasi: number;
    vazgecmeIadeGunSayisi: number;
    gecBildirimGunlukKesintiyUzdesi: number;
    noShowSuresiGun: number;
    aktifMi: boolean;
}

export interface KampKonaklamaTarifeYonetimDto {
    id?: number | null;
    kampProgramiId?: number;
    kod: string;
    ad: string;
    minimumKisi: number;
    maksimumKisi: number;
    kamuGunlukUcret: number;
    digerGunlukUcret: number;
    buzdolabiGunlukUcret: number;
    televizyonGunlukUcret: number;
    klimaGunlukUcret: number;
    aktifMi: boolean;
}

export interface KampPuanKuraliYonetimBaglamDto {
    programlar: KampProgramiSecenekDto[];
    globalBasvuruSahibiTipleri: KampPuanBasvuruSahibiTipSecenekDto[];
    programParametreAyarlari: KampProgramiParametreAyariDto[];
    konaklamaTarifeleri?: KampKonaklamaTarifeYonetimDto[];
    kuralSetleri: KampPuanKuralSetiDto[];
    basvuruSahibiTipleri: KampPuanBasvuruSahibiTipiDto[];
    katilimciTipleri: KampSecenekDto[];
    yasUcretKurali: KampYasUcretKuraliDto;
}

export interface KampDonemiAtamaBaglamDto {
    konaklamaTarifeleri: KampKonaklamaTarifeYonetimDto[];
}

export interface KampPuanKuraliYonetimKaydetRequestDto {
    kuralSetleri: KampPuanKuralSetiDto[];
    basvuruSahibiTipleri: KampPuanBasvuruSahibiTipiDto[];
    programParametreAyarlari: KampProgramiParametreAyariDto[];
    konaklamaTarifeleri?: KampKonaklamaTarifeYonetimDto[];
    yasUcretKurali: KampYasUcretKuraliDto;
}

export interface KampYasUcretKuraliDto {
    id?: number | null;
    ucretsizCocukMaxYas: number;
    yarimUcretliCocukMaxYas: number;
    yemekOrani: number;
    aktifMi: boolean;
}

export interface KampTesisDto {
    id: number;
    ad: string;
}

export interface KampDonemiDto {
    id?: number | null;
    kampProgramiId: number;
    kampProgramiAd?: string | null;
    kod: string;
    ad: string;
    yil: number;
    basvuruBaslangicTarihi: string;
    basvuruBitisTarihi: string;
    konaklamaBaslangicTarihi: string;
    konaklamaBitisTarihi: string;
    minimumGece: number;
    maksimumGece: number;
    onayGerektirirMi: boolean;
    cekilisGerekliMi: boolean;
    ayniAileIcinTekBasvuruMu: boolean;
    iptalSonGun?: string | null;
    aktifMi: boolean;
}

export interface KampDonemiYonetimBaglamDto {
    globalDonemYonetimiYapabilirMi: boolean;
    programlar: KampProgramiSecenekDto[];
    tesisler: KampTesisDto[];
}

export interface KampDonemiTesisAtamaDto {
    tesisId: number;
    tesisAd: string;
    atamaVarMi: boolean;
    donemdeAktifMi: boolean;
    basvuruyaAcikMi: boolean;
    toplamKontenjan: number;
    aciklama?: string | null;
    konaklamaTarifeKodlari: string[];
}

export interface KampBasvuruBaglamDto {
    donemler: KampBasvuruDonemSecenekDto[];
    basvuruSahibiTipleri: KampBasvuruSahibiTipSecenekDto[];
    katilimciTipleri: KampSecenekDto[];
    akrabalikTipleri: KampAkrabalikTipiSecenekDto[];
}

export interface KampBasvuruDonemSecenekDto {
    id: number;
    kampProgramiId: number;
    kampProgramiAd?: string | null;
    ad: string;
    yil: number;
    konaklamaBaslangicTarihi: string;
    konaklamaBitisTarihi: string;
    gecmisKatilimYillari: number[];
    tesisler: KampBasvuruTesisSecenekDto[];
}

export interface KampBasvuruTesisSecenekDto {
    tesisId: number;
    tesisAd: string;
    toplamKontenjan: number;
    birimler: KampKonaklamaBirimiSecenekDto[];
}

export interface KampKonaklamaBirimiSecenekDto {
    kod: string;
    ad: string;
    minimumKisi: number;
    maksimumKisi: number;
}

export interface KampSecenekDto {
    kod: string;
    ad: string;
}

export interface KampBasvuruSahibiTipSecenekDto extends KampSecenekDto {
    id: number;
    varsayilanKatilimciTipiKodu?: string | null;
}

export interface KampAkrabalikTipiSecenekDto extends KampSecenekDto {
    basvuruSahibiAkrabaligiMi: boolean;
}

export interface KampBasvuruKatilimciDto {
    id?: number | null;
    adSoyad: string;
    tcKimlikNo?: string | null;
    dogumTarihi: string;
    basvuruSahibiMi: boolean;
    katilimciTipi: string;
    akrabalikTipi: string;
    kimlikBilgileriDogrulandiMi: boolean;
    yemekTalepEdiyorMu: boolean;
}

export interface KampBasvuruRequestDto {
    kampDonemiId: number;
    tesisId: number;
    konaklamaBirimiTipi: string;
    basvuruSahibiTipi: string;
    hizmetYili: number;
    gecmisKatilimYillari: number[];
    evcilHayvanGetirecekMi: boolean;
    buzdolabiTalepEdildiMi: boolean;
    televizyonTalepEdildiMi: boolean;
    klimaTalepEdildiMi: boolean;
    katilimcilar: KampBasvuruKatilimciDto[];
}

export interface KampBasvuruOnizlemeDto {
    basvuruGecerliMi: boolean;
    oncelikSirasi: number;
    puan: number;
    gunlukToplamTutar: number;
    donemToplamTutar: number;
    avansToplamTutar: number;
    kalanOdemeTutari: number;
    kullanilanKontenjan: number;
    toplamKontenjan: number;
    bosKontenjan: number;
    kontenjanMesaji?: string | null;
    gecmisKatilimYillari: number[];
    hatalar: string[];
    uyarilar: string[];
}

export interface KampBasvuruDto {
    id: number;
    basvuruNo: string;
    kampDonemiId: number;
    kampDonemiAd: string;
    konaklamaBaslangicTarihi: string;
    konaklamaBitisTarihi: string;
    tesisId: number;
    tesisAd: string;
    konaklamaBirimiTipi: string;
    basvuruSahibiAdiSoyadi: string;
    basvuruSahibiTipi: string;
    hizmetYili: number;
    gecmisKatilimYillari: number[];
    evcilHayvanGetirecekMi: boolean;
    durum: string;
    katilimciSayisi: number;
    oncelikSirasi: number;
    puan: number;
    gunlukToplamTutar: number;
    donemToplamTutar: number;
    avansToplamTutar: number;
    kalanOdemeTutari: number;
    uyarilar: string[];
    buzdolabiTalepEdildiMi: boolean;
    televizyonTalepEdildiMi: boolean;
    klimaTalepEdildiMi: boolean;
    createdAt?: string | null;
    katilimcilar: KampBasvuruKatilimciDto[];
}

export interface KampKatilimciIptalSonucDto {
    kampBasvuruId: number;
    katilimciId: number;
    kalanKatilimciSayisi: number;
    tekKisiKaldiMi: boolean;
    uyariMesaji?: string | null;
}

export interface KampIadeHesaplamaRequestDto {
    basvuruDurumu: string;
    kampDonemiId?: number | null;
    kampBaslangicTarihi: string;
    toplamGunSayisi: number;
    vazgecmeTarihi?: string | null;
    avansTutari: number;
    donemToplamTutari: number;
    odenenToplamTutar: number;
    mazeretliZorunluAyrilisMi: boolean;
    kullanilmayanGunSayisi: number;
}

export interface KampIadeKarariDto {
    iadeVarMi: boolean;
    iadeTutari: number;
    kesintiTutari: number;
    gerekce: string;
}

export interface KampTahsisBaglamDto {
    donemler: KampTahsisDonemSecenekDto[];
    tesisler: KampTahsisTesisSecenekDto[];
    durumlar: string[];
}

export interface KampTahsisDonemSecenekDto {
    id: number;
    kampProgramiAd?: string | null;
    ad: string;
}

export interface KampTahsisTesisSecenekDto {
    id: number;
    ad: string;
}

export interface KampTahsisListeDto {
    id: number;
    siralama: number;
    kampDonemiId: number;
    kampDonemiAd: string;
    tesisId: number;
    tesisAd: string;
    basvuruSahibiAdiSoyadi: string;
    basvuruSahibiTipi: string;
    konaklamaBirimiTipi: string;
    durum: string;
    katilimciSayisi: number;
    oncelikSirasi: number;
    puan: number;
    donemToplamTutar: number;
    avansToplamTutar: number;
    toplamKontenjan: number;
    tahsisEdilenSayisi: number;
    kalanKontenjan: number;
    createdAt: string;
    uyarilar: string[];
}

export interface KampTahsisKararRequestDto {
    durum: string;
}

export interface KampTahsisOtomatikKararRequestDto {
    kampDonemiId: number;
    tesisId: number;
}

export interface KampTahsisOtomatikKararSonucDto {
    kampDonemiId: number;
    tesisId: number;
    toplamKontenjan: number;
    degerlendirilenBasvuruSayisi: number;
    tahsisEdilenSayisi: number;
    tahsisEdilemeyenSayisi: number;
    guncellenenKayitSayisi: number;
}

export interface KampRezervasyonBaglamDto {
    donemler: KampRezervasyonDonemSecenekDto[];
    tesisler: KampRezervasyonTesisSecenekDto[];
    durumlar: string[];
}

export interface KampRezervasyonDonemSecenekDto {
    id: number;
    kampProgramiAd?: string | null;
    ad: string;
}

export interface KampRezervasyonTesisSecenekDto {
    id: number;
    ad: string;
}

export interface KampRezervasyonListeDto {
    id: number;
    rezervasyonNo: string;
    kampBasvuruId: number;
    kampDonemiId: number;
    kampDonemiAd: string;
    tesisId: number;
    tesisAd: string;
    basvuruSahibiAdiSoyadi: string;
    basvuruSahibiTipi: string;
    konaklamaBirimiTipi: string;
    katilimciSayisi: number;
    donemToplamTutar: number;
    avansToplamTutar: number;
    durum: string;
    iptalNedeni?: string | null;
    iptalTarihi?: string | null;
    createdAt: string;
}

export interface KampRezervasyonUretSonucDto {
    id: number;
    rezervasyonNo: string;
}

export interface KampRezervasyonIptalRequestDto {
    iptalNedeni: string;
}

export interface KampNoShowIptalSonucDto {
    kampDonemiId: number;
    kampDonemiAd: string;
    degerlendirilenBasvuruSayisi: number;
    iptalEdilenSayisi: number;
}

export interface KampTarifeYonetimBaglamDto {
    programlar: KampProgramiSecenekDto[];
}

export interface KampTarifeKaydetRequestDto {
    tarifeler: KampKonaklamaTarifeYonetimDto[];
}

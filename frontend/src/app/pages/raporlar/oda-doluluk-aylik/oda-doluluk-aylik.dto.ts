export interface OdaDolulukOdaDto {
    odaId: number;
    odaNo: string;
    binaAdi: string | null;
    odaTipiAdi: string | null;
    kapasite: number;
}

export interface OdaDolulukCakismaDto {
    rezervasyonId: number;
    referansNo: string;
    misafirAdiSoyadi: string | null;
    girisTarihi: string;
    cikisTarihi: string;
    rezervasyonDurumu: string;
}

export interface OdaDolulukHucreDto {
    odaId: number;
    odaNo: string;
    doluMu: boolean;
    rezervasyonId: number | null;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;
    kurumUnite: string | null;
    kisiSayisi: number;
    girisTarihi: string | null;
    cikisTarihi: string | null;
    rezervasyonDurumu: string | null;
    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string | null;
    odemesiEksikMi: boolean;
    odaDegisimiGerekliMi: boolean;
    hucreRenkKodu: string | null;
    tutarAciklamasi: string | null;
    cakismaVarMi: boolean;
    cakismaSayisi: number;
    cakismalar: OdaDolulukCakismaDto[];
}

export interface OdaDolulukGunDto {
    tarih: string;
    gunAdi: string;
    hucreler: OdaDolulukHucreDto[];
}

export interface OdaDolulukOzetDto {
    toplamOdaSayisi: number;
    gunSayisi: number;
    toplamOdaGunSayisi: number;
    doluOdaGunSayisi: number;
    bosOdaGunSayisi: number;
    dolulukOraniYuzde: number;
    toplamTahsilat: number;
    toplamKalanTutar: number;
    ayIcindeTahsilEdilenTutar: number;
    konaklayanRezervasyonlarinToplamTahsilati: number;
    konaklayanRezervasyonlarinToplamKalanTutari: number;
}

export interface AylikOdaDolulukRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    yil: number;
    ay: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    odalar: OdaDolulukOdaDto[];
    gunler: OdaDolulukGunDto[];
    ozet: OdaDolulukOzetDto;
}

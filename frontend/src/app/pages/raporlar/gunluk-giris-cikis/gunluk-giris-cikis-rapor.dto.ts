export interface GunlukGirisCikisOzetDto {
    girisSayisi: number;
    cikisSayisi: number;
    devamEdenSayisi: number;
    gecikenCikisSayisi: number;
    toplamRezervasyonSayisi: number;
    toplamKisiSayisi: number;
    toplamKalanTutar: number;
    paraBirimi: string;
}

export interface GunlukGirisCikisRezervasyonDto {
    rezervasyonId: number;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;
    kurumUnite: string | null;

    girisTarihi: string;
    cikisTarihi: string;

    rezervasyonDurumu: string | null;
    rezervasyonDurumuLabel: string | null;

    odaNolari: string[];
    kisiSayisi: number;

    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string;

    girisYapacakMi: boolean;
    cikisYapacakMi: boolean;
    devamEdiyorMu: boolean;
    gecikenCikisMi: boolean;

    listeDurumu: string;
    listeDurumuLabel: string;
    aciklama: string | null;
}

export interface GunlukGirisCikisRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    tarih: string;
    listeTipi: string;
    baslik: string;

    ozet: GunlukGirisCikisOzetDto;
    rezervasyonlar: GunlukGirisCikisRezervasyonDto[];
}

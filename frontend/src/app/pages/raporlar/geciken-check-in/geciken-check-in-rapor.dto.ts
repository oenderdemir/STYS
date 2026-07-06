export interface GecikenCheckInOzetDto {
    toplamRezervasyonSayisi: number;
    bugunGirisSayisi: number;
    gecikenSayisi: number;
    kritikGecikenSayisi: number;
    toplamKisiSayisi: number;
    toplamKalanTutar: number;
}

export interface GecikenCheckInRezervasyonDto {
    rezervasyonId: number;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;
    misafirTelefon: string | null;

    girisTarihi: string;
    cikisTarihi: string;

    gecikenGunSayisi: number;
    kisiSayisi: number;

    rezervasyonDurumu: string;
    rezervasyonDurumuLabel: string;

    gecikmeDurumu: string;
    gecikmeDurumuLabel: string;

    odaNolari: string[];
    odaTipleri: string[];

    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string | null;
}

export interface GecikenCheckInRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    referansTarihi: string;
    odaTipiId: number | null;
    odaTipiAdi: string | null;
    gecikmeDurumu: string;
    baslik: string;

    ozet: GecikenCheckInOzetDto;
    rezervasyonlar: GecikenCheckInRezervasyonDto[];
}

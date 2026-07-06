export interface OrtalamaKonaklamaSuresiOzetDto {
    toplamRezervasyonSayisi: number;
    toplamKisiSayisi: number;
    toplamGeceSayisi: number;
    ortalamaGeceSayisi: number;
    enKisaKonaklamaGece: number;
    enUzunKonaklamaGece: number;

    kisaKonaklamaSayisi: number;
    ortaKonaklamaSayisi: number;
    uzunKonaklamaSayisi: number;
}

export interface OrtalamaKonaklamaSuresiOdaTipiDto {
    odaTipiId: number;
    odaTipiAdi: string;
    rezervasyonSayisi: number;
    toplamKisiSayisi: number;
    toplamGeceSayisi: number;
    ortalamaGeceSayisi: number;
    enKisaKonaklamaGece: number;
    enUzunKonaklamaGece: number;
}

export interface OrtalamaKonaklamaSuresiRezervasyonDto {
    rezervasyonId: number;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;

    girisTarihi: string;
    cikisTarihi: string;

    geceSayisi: number;
    kisiSayisi: number;

    odaNolari: string[];
    odaTipleri: string[];

    rezervasyonDurumu: string | null;
    rezervasyonDurumuLabel: string | null;

    konaklamaGrubu: string;
    konaklamaGrubuLabel: string;
}

export interface OrtalamaKonaklamaSuresiRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    baslangic: string;
    bitis: string;
    odaTipiId: number | null;
    odaTipiAdi: string | null;
    baslik: string;

    ozet: OrtalamaKonaklamaSuresiOzetDto;
    odaTipleri: OrtalamaKonaklamaSuresiOdaTipiDto[];
    rezervasyonlar: OrtalamaKonaklamaSuresiRezervasyonDto[];
}

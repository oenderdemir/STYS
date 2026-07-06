export interface RezervasyonDurumDagilimiOzetDto {
    toplamRezervasyonSayisi: number;

    taslakSayisi: number;
    onayliSayisi: number;
    checkInTamamlandiSayisi: number;
    checkOutTamamlandiSayisi: number;
    iptalSayisi: number;

    gerceklesenRezervasyonSayisi: number;
    devamEdenRezervasyonSayisi: number;

    iptalOrani: number;
    gerceklesmeOrani: number;
    checkInOrani: number;
    checkOutOrani: number;

    toplamKisiSayisi: number;
    toplamGeceSayisi: number;
}

export interface RezervasyonDurumDagilimiDurumSatiriDto {
    durum: string;
    durumLabel: string;
    rezervasyonSayisi: number;
    kisiSayisi: number;
    geceSayisi: number;
    oran: number;
}

export interface RezervasyonDurumDagilimiOdaTipiSatiriDto {
    odaTipiId: number;
    odaTipiAdi: string;
    rezervasyonSayisi: number;
    iptalSayisi: number;
    gerceklesenSayisi: number;
    kisiSayisi: number;
    geceSayisi: number;
    iptalOrani: number;
    gerceklesmeOrani: number;
}

export interface RezervasyonDurumDagilimiRezervasyonDto {
    rezervasyonId: number;
    referansNo: string | null;
    misafirAdiSoyadi: string | null;

    girisTarihi: string;
    cikisTarihi: string;

    geceSayisi: number;
    kisiSayisi: number;

    rezervasyonDurumu: string;
    rezervasyonDurumuLabel: string;

    odaNolari: string[];
    odaTipleri: string[];
}

export interface RezervasyonDurumDagilimiRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    baslangic: string;
    bitis: string;

    odaTipiId: number | null;
    odaTipiAdi: string | null;
    durum: string | null;
    durumLabel: string | null;

    baslik: string;

    ozet: RezervasyonDurumDagilimiOzetDto;
    durumlar: RezervasyonDurumDagilimiDurumSatiriDto[];
    odaTipleri: RezervasyonDurumDagilimiOdaTipiSatiriDto[];
    rezervasyonlar: RezervasyonDurumDagilimiRezervasyonDto[];
}

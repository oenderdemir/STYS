export interface EkHizmetDto {
    id?: number | null;
    tesisId: number;
    globalEkHizmetTanimiId?: number | null;
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    paketIcerikHizmetKodu: string | null;
    aktifMi: boolean;
}

export interface GlobalEkHizmetTanimiDto {
    id?: number | null;
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    paketIcerikHizmetKodu: string | null;
    aktifMi: boolean;
}

export interface EkHizmetTesisAtamaDto {
    globalEkHizmetTanimiId: number;
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    paketIcerikHizmetKodu: string | null;
    globalAktifMi: boolean;
    tesisteKullanilabilirMi: boolean;
    tarifeSayisi: number;
}

export interface EkHizmetTarifeDto {
    id?: number | null;
    tesisId: number;
    ekHizmetId: number | null;
    ekHizmetAdi: string;
    ekHizmetAciklama: string | null;
    birimAdi: string;
    birimFiyat: number;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aktifMi: boolean;
}

export interface EkHizmetTesisDto {
    id: number;
    ad: string;
}

export interface EkHizmetFormRow {
    id?: number | null;
    ekHizmetId: number | null;
    birimFiyat: number | null;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aktifMi: boolean;
}

export interface EkHizmetTanimFormRow {
    id?: number | null;
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    paketIcerikHizmetKodu: string | null;
    aktifMi: boolean;
}

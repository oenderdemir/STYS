export interface EkHizmetTarifeDto {
    id?: number | null;
    tesisId: number;
    ad: string;
    aciklama: string | null;
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
    ad: string;
    aciklama: string | null;
    birimAdi: string;
    birimFiyat: number | null;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aktifMi: boolean;
}

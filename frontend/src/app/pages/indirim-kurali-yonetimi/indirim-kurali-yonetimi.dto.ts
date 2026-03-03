export interface IndirimKuraliDto {
    id?: number | null;
    kod: string;
    ad: string;
    indirimTipi: string;
    deger: number;
    kapsamTipi: string;
    tesisId?: number | null;
    baslangicTarihi: string;
    bitisTarihi: string;
    oncelik: number;
    birlesebilirMi: boolean;
    aktifMi: boolean;
    misafirTipiIds: number[];
    konaklamaTipiIds: number[];
}

export interface KonaklamaTipiLookupDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

export interface MisafirTipiLookupDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

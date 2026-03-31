export interface OdaFiyatDto {
    id?: number | null;
    tesisOdaTipiId: number;
    konaklamaTipiId: number;
    misafirTipiId: number;
    kisiSayisi: number;
    kullanimSekli: string;
    fiyat: number;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aktifMi: boolean;
}

export interface OdaTipiDto {
    id?: number | null;
    tesisId: number;
    odaSinifiId: number;
    ad: string;
    paylasimliMi: boolean;
    kapasite: number;
    aktifMi: boolean;
}

export interface KonaklamaTipiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

export interface MisafirTipiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

export interface OdaFiyatFormRow {
    id?: number | null;
    konaklamaTipiId: number | null;
    misafirTipiId: number | null;
    kullanimSekli: string;
    fiyat: number | null;
    paraBirimi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aktifMi: boolean;
    uiYeniOzelKullanimSatiriMi?: boolean;
}

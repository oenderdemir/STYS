export interface OdaKullanimBlokDto {
    id?: number | null;
    tesisId: number;
    odaId: number;
    blokTipi: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    aciklama: string | null;
    aktifMi: boolean;
}

export interface OdaKullanimBlokTesisDto {
    id: number;
    ad: string;
}

export interface OdaKullanimBlokOdaSecenekDto {
    id: number;
    tesisId: number;
    odaNo: string;
    binaAdi: string;
    odaTipiAdi: string;
}

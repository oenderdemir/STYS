export interface IsletmeAlaniDto {
    id?: number | null;
    ad: string;
    binaId: number;
    isletmeAlaniSinifiId: number;
    isletmeAlaniSinifiAd?: string | null;
    ozelAd?: string | null;
    aktifMi: boolean;
}

export interface IsletmeAlaniSinifiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

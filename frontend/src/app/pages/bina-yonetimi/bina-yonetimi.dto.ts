export interface BinaIsletmeAlaniDto {
    id?: number | null;
    isletmeAlaniSinifiId: number;
    isletmeAlaniSinifiAd?: string | null;
    ozelAd?: string | null;
    aktifMi: boolean;
}

export interface BinaDto {
    id?: number | null;
    ad: string;
    tesisId: number;
    katSayisi: number;
    aktifMi: boolean;
    yoneticiUserIds?: string[] | null;
    isletmeAlanlari?: BinaIsletmeAlaniDto[] | null;
}

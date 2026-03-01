export interface BinaDto {
    id?: number | null;
    ad: string;
    tesisId: number;
    katSayisi: number;
    aktifMi: boolean;
    yoneticiUserIds?: string[] | null;
}

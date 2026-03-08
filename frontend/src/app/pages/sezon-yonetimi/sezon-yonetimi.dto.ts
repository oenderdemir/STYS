export interface SezonKuraliDto {
    id?: number | null;
    tesisId: number;
    kod: string;
    ad: string;
    baslangicTarihi: string;
    bitisTarihi: string;
    minimumGece: number;
    stopSaleMi: boolean;
    aktifMi: boolean;
}

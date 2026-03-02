export interface OdaOzellikDegerDto {
    odaOzellikId: number;
    deger?: string | null;
}

export interface OdaDto {
    id?: number | null;
    odaNo: string;
    binaId: number;
    tesisOdaTipiId: number;
    katNo: number;
    odaOzellikDegerleri: OdaOzellikDegerDto[];
    aktifMi: boolean;
}

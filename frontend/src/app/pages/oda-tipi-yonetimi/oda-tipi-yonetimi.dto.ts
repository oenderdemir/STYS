export interface TesisOdaTipiOzellikDegerDto {
    odaOzellikId: number;
    deger?: string | null;
}

export interface OdaTipiDto {
    id?: number | null;
    tesisId: number;
    odaSinifiId: number;
    ad: string;
    paylasimliMi: boolean;
    kapasite: number;
    odaOzellikDegerleri: TesisOdaTipiOzellikDegerDto[];
    aktifMi: boolean;
}

export interface OdaSinifiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

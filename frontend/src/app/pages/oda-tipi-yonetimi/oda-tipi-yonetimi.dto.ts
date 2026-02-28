export interface OdaTipiDto {
    id?: number | null;
    tesisId: number;
    odaSinifiId: number;
    ad: string;
    paylasimliMi: boolean;
    kapasite: number;
    balkonVarMi: boolean;
    klimaVarMi: boolean;
    metrekare?: number | null;
    aktifMi: boolean;
}

export interface OdaSinifiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

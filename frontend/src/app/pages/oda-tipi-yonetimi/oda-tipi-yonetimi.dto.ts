export interface OdaTipiDto {
    id?: number | null;
    ad: string;
    paylasimliMi: boolean;
    kapasite: number;
    balkonVarMi: boolean;
    klimaVarMi: boolean;
    metrekare?: number | null;
    aktifMi: boolean;
}

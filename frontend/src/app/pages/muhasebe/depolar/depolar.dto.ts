export interface DepoModel {
    id?: number;
    tesisId?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateDepoRequest extends Omit<DepoModel, 'id'> {}
export interface UpdateDepoRequest extends Omit<DepoModel, 'id'> {}

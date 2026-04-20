export interface PaketTuruModel {
    id?: number;
    ad: string;
    kisaAd: string;
    aktifMi: boolean;
}

export interface CreatePaketTuruRequest extends Omit<PaketTuruModel, 'id'> {}
export interface UpdatePaketTuruRequest extends Omit<PaketTuruModel, 'id'> {}

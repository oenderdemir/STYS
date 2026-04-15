export interface TasinirKartModel {
    id?: number;
    tasinirKodId: number;
    stokKodu: string;
    ad: string;
    birim: string;
    malzemeTipi: string;
    sarfMi: boolean;
    demirbasMi: boolean;
    takipliMi: boolean;
    kdvOrani: number;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateTasinirKartRequest extends Omit<TasinirKartModel, 'id'> {}
export interface UpdateTasinirKartRequest extends Omit<TasinirKartModel, 'id'> {}

export const MALZEME_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Sarf', value: 'Sarf' },
    { label: 'Demirbas', value: 'Demirbas' },
    { label: 'Tuketim', value: 'Tuketim' },
    { label: 'Ticari Mal', value: 'TicariMal' },
    { label: 'Diger', value: 'Diger' }
];

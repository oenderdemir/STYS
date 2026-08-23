export interface TasinirKartModel {
    id?: number;
    tesisId?: number | null;
    tasinirKodId: number;
    varsayilanDepoId?: number | null;
    stokKodu: string;
    muhasebeHesapPlaniId?: number | null;
    anaMuhasebeHesapKodu?: string | null;
    muhasebeHesapSiraNo?: number | null;
    ad: string;
    birim: string;
    malzemeTipi: string;
    sarfMi: boolean;
    demirbasMi: boolean;
    takipliMi: boolean;
    takipTipi: string;
    kdvOrani: number;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateTasinirKartRequest extends Omit<TasinirKartModel, 'id' | 'stokKodu' | 'muhasebeHesapPlaniId' | 'anaMuhasebeHesapKodu' | 'muhasebeHesapSiraNo'> {
    stokKodu?: string | null;
}
export interface UpdateTasinirKartRequest extends Omit<TasinirKartModel, 'id' | 'stokKodu' | 'muhasebeHesapPlaniId' | 'anaMuhasebeHesapKodu' | 'muhasebeHesapSiraNo'> {
    stokKodu?: string | null;
}

export const MALZEME_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Sarf', value: 'Sarf' },
    { label: 'Demirbas', value: 'Demirbas' },
    { label: 'Tuketim', value: 'Tuketim' },
    { label: 'Ticari Mal', value: 'TicariMal' },
    { label: 'Diger', value: 'Diger' }
];

export const TAKIP_TIPI_SECENEKLERI: Array<{ label: string; value: string }> = [
    { label: 'Takipsiz', value: 'Yok' },
    { label: 'Lot- parti', value: 'Lot' },
    { label: 'Seri', value: 'Seri' }
];

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

export interface PaketTuruOptionModel {
    id: number;
    ad: string;
    kisaAd: string;
    aktifMi: boolean;
}

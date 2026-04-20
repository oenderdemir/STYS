export interface HesapModel {
    id?: number;
    tesisId?: number | null;
    ad: string;
    muhasebeHesapPlaniId: number;
    muhasebeTamKod?: string | null;
    muhasebeHesapAdi?: string | null;
    genelHesapMi: boolean;
    muhasebeFormu?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
    kasaHesapIds: number[];
    bankaHesapIds: number[];
    depoIds: number[];
}

export interface HesapLookupModel {
    id: number;
    kod: string;
    ad: string;
}

export interface CreateHesapRequest extends Omit<HesapModel, 'id' | 'muhasebeTamKod' | 'muhasebeHesapAdi'> {}
export interface UpdateHesapRequest extends Omit<HesapModel, 'id' | 'muhasebeTamKod' | 'muhasebeHesapAdi'> {}

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

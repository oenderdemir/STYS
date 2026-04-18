export interface MuhasebeHesapPlaniModel {
    id?: number;
    kod: string;
    tamKod: string;
    ad: string;
    seviyeNo: number;
    ustHesapId?: number | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateMuhasebeHesapPlaniRequest extends Omit<MuhasebeHesapPlaniModel, 'id'> {}
export interface UpdateMuhasebeHesapPlaniRequest extends Omit<MuhasebeHesapPlaniModel, 'id'> {}

export interface MuhasebeHesapPlaniModel {
    id?: number;
    kod: string;
    tamKod: string;
    ad: string;
    seviyeNo: number;
    hesapTipi?: number;
    kurumId?: number | null;
    tesisId?: number | null;
    ustHesapId?: number | null;
    hasChildren?: boolean;
    aktifMi: boolean;
    detayHesapMi?: boolean;
    hareketGorebilirMi?: boolean;
    aciklama?: string | null;
}

export interface CreateMuhasebeHesapPlaniRequest extends Omit<MuhasebeHesapPlaniModel, 'id'> {}
export interface UpdateMuhasebeHesapPlaniRequest extends Omit<MuhasebeHesapPlaniModel, 'id'> {}

export interface MuhasebeDetayHesapOlusturRequest {
    ad: string;
}

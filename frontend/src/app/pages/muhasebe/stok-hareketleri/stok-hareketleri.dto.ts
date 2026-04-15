export interface StokHareketModel {
    id?: number;
    depoId: number;
    tasinirKartId: number;
    hareketTarihi: string;
    hareketTipi: string;
    miktar: number;
    birimFiyat: number;
    tutar: number;
    belgeNo?: string | null;
    belgeTarihi?: string | null;
    aciklama?: string | null;
    cariKartId?: number | null;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    durum: string;
}

export interface CreateStokHareketRequest extends Omit<StokHareketModel, 'id' | 'tutar'> {}
export interface UpdateStokHareketRequest extends Omit<StokHareketModel, 'id' | 'tutar'> {}

export interface StokBakiyeModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
}

export interface StokKartOzetModel {
    tasinirKartId: number;
    stokKodu: string;
    ad: string;
    birim: string;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
}

export const STOK_HAREKET_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Giris', value: 'Giris' },
    { label: 'Cikis', value: 'Cikis' },
    { label: 'Transfer', value: 'Transfer' },
    { label: 'Iade', value: 'Iade' },
    { label: 'Sarf', value: 'Sarf' },
    { label: 'Sayim Farki', value: 'SayimFarki' },
    { label: 'Zimmet', value: 'Zimmet' }
];

export const STOK_HAREKET_DURUMLARI: Array<{ label: string; value: string }> = [
    { label: 'Aktif', value: 'Aktif' },
    { label: 'Iptal', value: 'Iptal' }
];

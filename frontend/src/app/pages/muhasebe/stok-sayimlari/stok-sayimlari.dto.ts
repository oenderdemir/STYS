export interface StokSayimSatirModel {
    id?: number;
    stokSayimId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    takipTipi: string;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    seriNo?: string | null;
    sistemMiktari: number;
    sayilanMiktar: number;
    farkMiktari: number;
}

export interface StokSayimModel {
    id?: number;
    tesisId: number;
    depoId: number;
    sayimTarihi: string;
    durum: string;
    aciklama?: string | null;
    satirlar: StokSayimSatirModel[];
}

export interface CreateStokSayimRequest {
    depoId: number;
    sayimTarihi: string;
    aciklama?: string | null;
}

export interface UpdateStokSayimSatirlarRequest {
    satirlar: Array<{ id: number; sayilanMiktar: number }>;
}

export interface AddStokSayimSatirRequest {
    tasinirKartId: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    seriNo?: string | null;
    sayilanMiktar: number;
}

export const STOK_SAYIM_DURUMLARI: Array<{ label: string; value: string }> = [
    { label: 'Taslak', value: 'Taslak' },
    { label: 'Kesinlesti', value: 'Kesinlesti' },
    { label: 'Iptal', value: 'Iptal' }
];

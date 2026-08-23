export interface StokTalepSatirModel {
    id?: number;
    stokTalepId: number;
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
    talepMiktari: number;
    onaylananMiktar: number;
    teslimEdilenMiktar: number;
    aciklama?: string | null;
    transferGrupId?: string | null;
}

export interface StokTalepModel {
    id?: number;
    tesisId: number;
    talepEdenDepoId: number;
    karsilayanDepoId: number;
    talepTarihi: string;
    durum: string;
    aciklama?: string | null;
    talepEdenKullaniciId?: string | null;
    satirlar: StokTalepSatirModel[];
}

export interface CreateStokTalepRequest {
    talepEdenDepoId: number;
    karsilayanDepoId: number;
    talepTarihi: string;
    aciklama?: string | null;
}

export interface AddStokTalepSatirRequest {
    tasinirKartId: number;
    talepMiktari: number;
    aciklama?: string | null;
}

export interface UpdateStokTalepSatirlarRequest {
    satirlar: Array<{ id: number; talepMiktari: number; onaylananMiktar: number; aciklama?: string | null }>;
}

export interface TeslimEtStokTalepRequest {
    satirlar: Array<{ id: number; stokLotId?: number | null; stokSeriId?: number | null }>;
}

export const STOK_TALEP_DURUMLARI: Array<{ label: string; value: string }> = [
    { label: 'Taslak', value: 'Taslak' },
    { label: 'Bekliyor', value: 'Bekliyor' },
    { label: 'Onaylandi', value: 'Onaylandi' },
    { label: 'Kismi Onaylandi', value: 'KismiOnaylandi' },
    { label: 'Reddedildi', value: 'Reddedildi' },
    { label: 'Teslim Edildi', value: 'TeslimEdildi' },
    { label: 'Iptal', value: 'Iptal' }
];

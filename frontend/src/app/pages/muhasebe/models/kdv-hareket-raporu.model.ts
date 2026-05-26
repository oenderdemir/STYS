import { BELGE_YONU_SECENEKLERI, KdvRaporFilterModel } from './kdv-ozet-raporu.model';

export type KdvHareketRaporFilterModel = KdvRaporFilterModel;

export interface KdvHareketRaporModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    toplamKayitSayisi: number;
    ozet: KdvHareketRaporOzetModel;
    satirlar: KdvHareketRaporSatirModel[];
}

export interface KdvHareketRaporOzetModel {
    satisKayitSayisi: number;
    alisKayitSayisi: number;
    iadeKayitSayisi: number;
    istisnaKayitSayisi: number;
    tevkifatKayitSayisi: number;
    toplamMatrah: number;
    toplamKdvTutari: number;
}

export interface KdvHareketRaporSatirModel {
    belgeId: number;
    belgeNo: string;
    belgeTarihi: string;
    belgeTipi: string;
    islemYonu: string;
    satirId: number;
    satirAciklama: string;
    matrah: number;
    kdvOrani: number;
    kdvTutari: number;
    kdvUygulamaTipi: string;
    kdvIstisnaTanimId?: number | null;
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    tevkifatPay?: number | null;
    tevkifatPayda?: number | null;
    tevkifatTutari: number;
}

export interface TevkifatHareketRaporModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    toplamKayitSayisi: number;
    ozet: TevkifatHareketRaporOzetModel;
    satirlar: TevkifatHareketRaporSatirModel[];
}

export interface TevkifatHareketRaporOzetModel {
    satisKayitSayisi: number;
    alisKayitSayisi: number;
    toplamMatrah: number;
    toplamTevkifatTutari: number;
}

export interface TevkifatHareketRaporSatirModel {
    belgeId: number;
    belgeNo: string;
    belgeTarihi: string;
    belgeTipi: string;
    islemYonu: string;
    satirId: number;
    satirAciklama: string;
    matrah: number;
    kdvTutari: number;
    tevkifatPay: number;
    tevkifatPayda: number;
    tevkifatTutari: number;
}

export const KDV_UYGULAMA_TIPI_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Tümü', value: null },
    { label: 'KDV\'li', value: 1 },
    { label: 'Tam İstisna', value: 2 },
    { label: 'Kısmi İstisna', value: 3 },
    { label: 'KDV Kapsam Dışı', value: 4 },
    { label: 'Tevkifatlı', value: 5 }
];

export const MUS_FIS_DURUMU_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Fişi Olan', value: 'FisiOlan' },
    { label: 'Fişi Olmayan', value: 'FisiOlmayan' }
];

export { BELGE_YONU_SECENEKLERI };
export type { KdvRaporFilterModel } from './kdv-ozet-raporu.model';
export { createDefaultKdvRaporFilter } from './kdv-ozet-raporu.model';

import { DONEM_SECENEKLERI, getMaliYilSecenekleri, getDonemLabel } from './kdv-ozet-raporu.model';

export interface KdvBeyannameHazirlikKontrolFilterModel {
    tesisId?: number | null;
    depoId?: number | null;
    maliYil: number;
    donem: number;
    baslangicTarihi?: string | null;
    bitisTarihi?: string | null;
}

export function createDefaultKdvBeyannameHazirlikKontrolFilter(): KdvBeyannameHazirlikKontrolFilterModel {
    const now = new Date();
    return {
        tesisId: null,
        depoId: null,
        maliYil: now.getFullYear(),
        donem: now.getMonth() + 1,
        baslangicTarihi: null,
        bitisTarihi: null
    };
}

export interface KdvBeyannameHazirlikKontrolModel {
    tesisId?: number | null;
    depoId?: number | null;
    maliYil: number;
    donem: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    beyanaHazirMi: boolean;
    toplamKontrolSayisi: number;
    basariliKontrolSayisi: number;
    uyariliKontrolSayisi: number;
    bloklayiciKontrolSayisi: number;
    hesaplananKdvTutari: number;
    indirilecekKdvTutari: number;
    netKdv: number;
    kontroller: KdvBeyannameHazirlikKontrolMaddesiModel[];
}

export interface KdvBeyannameHazirlikKontrolMaddesiModel {
    kod: string;
    baslik: string;
    aciklama: string;
    /** Basarili / Uyari / Bloklayici */
    durum: string;
    /** success / info / warn / error */
    severity: string;
    bloklayiciMi: boolean;
    etkilenenKayitSayisi?: number | null;
    route?: string | null;
    routeQueryParams?: any | null;
}

// Re-export utilities from kdv-ozet-raporu.model for convenience
export { DONEM_SECENEKLERI, getMaliYilSecenekleri, getDonemLabel };

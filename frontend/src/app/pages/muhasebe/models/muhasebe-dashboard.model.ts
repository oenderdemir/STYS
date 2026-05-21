export interface MuhasebeDashboardFilterModel {
    tesisId?: number | null;
    maliYil?: number | null;
    donem?: number | null;
}

export function createDefaultDashboardFilter(): MuhasebeDashboardFilterModel {
    const now = new Date();
    return {
        tesisId: null,
        maliYil: now.getFullYear(),
        donem: null
    };
}

export interface MuhasebeDashboardModel {
    tesisId?: number | null;
    maliYil: number;
    donem?: number | null;
    acikDonemSayisi: number;
    kapaliDonemSayisi: number;
    taslakFisSayisi: number;
    onayliFisSayisi: number;
    iptalFisSayisi: number;
    tersKayitFisSayisi: number;
    dengesizTaslakFisSayisi: number;
    toplamBorc: number;
    toplamAlacak: number;
    fark: number;
    acikDonemler: MuhasebeDashboardDonemOzetModel[];
    sonFisler: MuhasebeDashboardFisOzetModel[];
    uyarilar: MuhasebeDashboardUyariModel[];
}

export interface MuhasebeDashboardDonemOzetModel {
    id: number;
    tesisId: number;
    maliYil: number;
    donemNo: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    kapaliMi: boolean;
}

export interface MuhasebeDashboardFisOzetModel {
    id: number;
    tesisId: number;
    fisNo: string;
    yevmiyeNo?: number | null;
    fisTarihi: string;
    maliYil: number;
    donem: number;
    fisTipi: string;
    durum: string;
    toplamBorc: number;
    toplamAlacak: number;
    aciklama?: string | null;
}

export interface MuhasebeDashboardUyariModel {
    tip: string;
    mesaj: string;
    route?: string | null;
    severity: string;
}

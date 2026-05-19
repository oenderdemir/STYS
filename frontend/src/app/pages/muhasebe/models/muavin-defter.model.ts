export interface MuavinDefterFilterModel {
    tesisId: number | null;
    muhasebeHesapPlaniId: number | null;
    baslangicTarihi: string | null;
    bitisTarihi: string | null;
    maliYil: number | null;
    donem: number | null;
    altHesaplariDahilEt: boolean;
    page: number;
    pageSize: number;
}

export function createDefaultMuavinDefterFilter(): MuavinDefterFilterModel {
    const currentYear = new Date().getFullYear();
    return {
        tesisId: null,
        muhasebeHesapPlaniId: null,
        baslangicTarihi: null,
        bitisTarihi: null,
        maliYil: currentYear,
        donem: null,
        altHesaplariDahilEt: false,
        page: 1,
        pageSize: 50
    };
}

export function normalizeMuavinDefterFilter(filter: MuavinDefterFilterModel): MuavinDefterFilterModel {
    const normalized = { ...filter };
    if (normalized.page < 1) {
        normalized.page = 1;
    }
    if (normalized.pageSize < 1) {
        normalized.pageSize = 50;
    }
    return normalized;
}

export interface MuavinDefterSatirModel {
    fisId: number;
    fisNo: string;
    yevmiyeNo: number | null;
    fisTarihi: string | null;
    fisTipi: string;
    durum: string;
    siraNo: number;
    muhasebeHesapPlaniId: number;
    muhasebeHesapKodu: string;
    muhasebeHesapAdi: string;
    borc: number;
    alacak: number;
    satirAciklama: string;
    fisAciklama: string;
    bakiye: number;
    bakiyeTipi: string;
    kaynakModul: string | null;
    kaynakId: number | null;
}

export interface MuavinDefterModel {
    tesisId: number;
    muhasebeHesapPlaniId: number;
    muhasebeHesapKodu: string;
    muhasebeHesapAdi: string;
    toplamBorc: number;
    toplamAlacak: number;
    bakiye: number;
    bakiyeTipi: string;
    satirlar: MuavinDefterSatirModel[];
}

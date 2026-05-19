export interface MuhasebeFisFilterModel {
    tesisId: number | null;
    maliYil: number | null;
    donem: number | null;
    baslangicTarihi: string | null;
    bitisTarihi: string | null;
    fisTipi: string | null;
    durum: string | null;
    kaynakModul: string | null;
    yevmiyeNoBaslangic: number | null;
    yevmiyeNoBitis: number | null;
    fisNo: string | null;
    aciklama: string | null;
    page: number;
    pageSize: number;
}

export function createDefaultFisFilter(): MuhasebeFisFilterModel {
    const today = new Date();
    const year = today.getFullYear();
    const month = today.getMonth() + 1;
    const baslangic = new Date(year, 0, 1);
    const bitis = new Date(year, 11, 31);

    return {
        tesisId: null,
        maliYil: year,
        donem: null,
        baslangicTarihi: baslangic.toISOString().split('T')[0],
        bitisTarihi: bitis.toISOString().split('T')[0],
        fisTipi: null,
        durum: null,
        kaynakModul: null,
        yevmiyeNoBaslangic: null,
        yevmiyeNoBitis: null,
        fisNo: null,
        aciklama: null,
        page: 1,
        pageSize: 50
    };
}

export function normalizeFisFilter(filter: MuhasebeFisFilterModel): MuhasebeFisFilterModel {
    const normalized: MuhasebeFisFilterModel = {
        tesisId: filter.tesisId ?? null,
        maliYil: filter.maliYil ?? null,
        donem: filter.donem ?? null,
        baslangicTarihi: filter.baslangicTarihi || null,
        bitisTarihi: filter.bitisTarihi || null,
        fisTipi: filter.fisTipi || null,
        durum: filter.durum || null,
        kaynakModul: filter.kaynakModul || null,
        yevmiyeNoBaslangic: filter.yevmiyeNoBaslangic ?? null,
        yevmiyeNoBitis: filter.yevmiyeNoBitis ?? null,
        fisNo: (filter.fisNo || '').trim() || null,
        aciklama: (filter.aciklama || '').trim() || null,
        page: filter.page < 1 ? 1 : filter.page,
        pageSize: filter.pageSize < 1 ? 50 : filter.pageSize > 500 ? 500 : filter.pageSize
    };
    return normalized;
}

export const MuhasebeFisDurumlari = {
    Taslak: 'Taslak',
    Onayli: 'Onayli',
    Iptal: 'Iptal',
    TersKayit: 'TersKayit'
} as const;

export interface MuhasebeFisSatirModel {
    id: number;
    muhasebeFisId: number;
    muhasebeHesapPlaniId: number;
    muhasebeHesapKodu: string | null;
    muhasebeHesapAdi: string | null;
    siraNo: number;
    borc: number;
    alacak: number;
    paraBirimi: string;
    kur: number;
    cariKartId: number | null;
    tasinirKartId: number | null;
    depoId: number | null;
    kasaBankaHesapId: number | null;
    aciklama: string | null;
}

export interface MuhasebeFisModel {
    id: number;
    tesisId: number;
    maliYil: number;
    donem: number;
    fisNo: string;
    yevmiyeNo: number | null;
    fisTarihi: string;
    fisTipi: string;
    kaynakModul: string;
    kaynakId: number | null;
    durum: string;
    toplamBorc: number;
    toplamAlacak: number;
    aciklama: string | null;
    tersKayitFisId: number | null;
    iptalEdilenFisId: number | null;
    satirlar: MuhasebeFisSatirModel[];
}

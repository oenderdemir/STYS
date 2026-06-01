export interface MuhasebeFisFilterModel {
    tesisId: number | null;
    maliYil: number | null;
    donem: number | null;
    baslangicTarihi: string | Date | null;
    bitisTarihi: string | Date | null;
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
    const baslangic = new Date(year, 0, 1);
    const bitis = new Date(year, 11, 31);

    return {
        tesisId: null,
        maliYil: year,
        donem: null,
        baslangicTarihi: baslangic,
        bitisTarihi: bitis,
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
        baslangicTarihi: formatDateForApi(filter.baslangicTarihi),
        bitisTarihi: formatDateForApi(filter.bitisTarihi),
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

export function parseApiDate(value: string | Date | null | undefined): Date | null {
    if (!value) {
        return null;
    }

    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? null : value;
    }

    const trimmed = value.trim();
    if (!trimmed) {
        return null;
    }

    const dottedParts = trimmed.split('.');
    if (dottedParts.length === 3) {
        const day = Number(dottedParts[0]);
        const month = Number(dottedParts[1]);
        const year = Number(dottedParts[2]);
        if (Number.isFinite(day) && Number.isFinite(month) && Number.isFinite(year)) {
            return new Date(year, month - 1, day);
        }
    }

    const slashParts = trimmed.split('/');
    if (slashParts.length === 3) {
        const day = Number(slashParts[0]);
        const month = Number(slashParts[1]);
        const year = Number(slashParts[2]);
        if (Number.isFinite(day) && Number.isFinite(month) && Number.isFinite(year)) {
            return new Date(year, month - 1, day);
        }
    }

    const parts = trimmed.split('-');
    if (parts.length !== 3) {
        const parsed = new Date(trimmed);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    const year = Number(parts[0]);
    const month = Number(parts[1]);
    const day = Number(parts[2]);
    if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
        return null;
    }

    return new Date(year, month - 1, day);
}

export function formatDateForApi(value: string | Date | null | undefined): string | null {
    const date = parseApiDate(value);
    if (!date) {
        return null;
    }

    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
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

export interface UpdateMuhasebeFisSatirRequestModel {
    muhasebeHesapPlaniId: number;
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

export interface UpdateMuhasebeFisRequestModel {
    tesisId: number;
    fisTarihi: string;
    maliYil: number;
    donem: number;
    fisTipi: string;
    aciklama: string | null;
    satirlar: UpdateMuhasebeFisSatirRequestModel[];
}

export interface CreateMuhasebeFisSatirRequestModel {
    muhasebeHesapPlaniId: number;
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

export interface CreateMuhasebeFisRequestModel {
    tesisId: number;
    maliYil: number;
    donem: number;
    fisTarihi: string;
    fisTipi: string;
    kaynakModul: string | null;
    kaynakId: number | null;
    aciklama: string | null;
    satirlar: CreateMuhasebeFisSatirRequestModel[];
}

export const MuhasebeFisTipleri = {
    Mahsup: 'Mahsup',
    Tahsil: 'Tahsil',
    Tediye: 'Tediye',
    Acilis: 'Acilis',
    Kapanis: 'Kapanis',
    Duzeltme: 'Duzeltme'
} as const;


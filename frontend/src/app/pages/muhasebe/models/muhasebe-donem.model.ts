export interface MuhasebeDonemDto {
    id: number;
    tesisId: number;
    tesisAdi: string | null;
    maliYil: number;
    donemNo: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    kapaliMi: boolean;
    kapanisTarihi: string | null;
    aciklama: string | null;
}

export interface CreateMuhasebeDonemRequest {
    tesisId: number;
    maliYil: number;
    donemNo: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    aciklama: string | null;
}

export interface UpdateMuhasebeDonemRequest {
    tesisId: number;
    maliYil: number;
    donemNo: number;
    baslangicTarihi: string;
    bitisTarihi: string;
    kapaliMi: boolean;
    aciklama: string | null;
}

export function createDefaultDonemFilter(): {
    tesisId: number | null;
    maliYil: number | null;
    kapaliMi: boolean | null;
} {
    const today = new Date();
    return {
        tesisId: null,
        maliYil: today.getFullYear(),
        kapaliMi: null
    };
}

export interface KonaklamaVergisiHesapEslemeModel {
    id?: number;
    tesisId?: number | null;
    tesisAdi?: string | null;
    vergiHesapId: number;
    vergiHesapKodu?: string | null;
    vergiHesapAdi?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface KonaklamaVergisiHesapSecenekModel {
    id: number;
    kod: string;
    ad: string;
}

export interface CreateKonaklamaVergisiHesapEslemeRequest {
    tesisId?: number | null;
    vergiHesapId: number;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface UpdateKonaklamaVergisiHesapEslemeRequest extends CreateKonaklamaVergisiHesapEslemeRequest {}

export interface KonaklamaVergisiHesapEslemeFilterDto {
    tesisId?: number | null;
    aktifMi?: boolean | null;
}

export function createDefaultKonaklamaVergisiHesapEslemeFilter(): KonaklamaVergisiHesapEslemeFilterDto {
    return {
        tesisId: null,
        aktifMi: null
    };
}


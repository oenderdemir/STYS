export type TevkifatIslemYonu = 'Satis' | 'Alis';

export const TEVKIFAT_ISLEM_YONLERI: Array<{ label: string; value: TevkifatIslemYonu }> = [
    { label: 'Satış', value: 'Satis' },
    { label: 'Alış', value: 'Alis' }
];

export interface TevkifatHesapEslemeModel {
    id?: number;
    tesisId?: number | null;
    tesisAdi?: string | null;
    islemYonu: TevkifatIslemYonu;
    tevkifatPay: number;
    tevkifatPayda: number;
    muhasebeHesapPlaniId: number;
    muhasebeHesapKodu?: string | null;
    muhasebeHesapAdi?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateTevkifatHesapEslemeRequest {
    tesisId?: number | null;
    islemYonu: TevkifatIslemYonu;
    tevkifatPay: number;
    tevkifatPayda: number;
    muhasebeHesapPlaniId: number;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface UpdateTevkifatHesapEslemeRequest extends CreateTevkifatHesapEslemeRequest {}

export interface TevkifatHesapEslemeFilterDto {
    tesisId?: number | null;
    islemYonu?: TevkifatIslemYonu | null;
    aktifMi?: boolean | null;
}

export function createDefaultTevkifatHesapEslemeFilter(): TevkifatHesapEslemeFilterDto {
    return {
        tesisId: null,
        islemYonu: null,
        aktifMi: null
    };
}

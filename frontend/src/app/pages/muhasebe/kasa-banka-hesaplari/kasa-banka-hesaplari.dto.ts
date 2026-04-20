export type KasaBankaHesapTipi = 'NakitKasa' | 'Banka';

export interface KasaBankaHesapModel {
    id?: number;
    tesisId?: number | null;
    tip: KasaBankaHesapTipi;
    kod: string;
    ad: string;
    muhasebeHesapPlaniId: number;
    muhasebeTamKod?: string | null;
    muhasebeHesapAdi?: string | null;
    bankaAdi?: string | null;
    subeAdi?: string | null;
    hesapNo?: string | null;
    iban?: string | null;
    musteriNo?: string | null;
    hesapTuru?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateKasaBankaHesapRequest extends Omit<KasaBankaHesapModel, 'id' | 'muhasebeTamKod' | 'muhasebeHesapAdi'> {}
export interface UpdateKasaBankaHesapRequest extends Omit<KasaBankaHesapModel, 'id' | 'muhasebeTamKod' | 'muhasebeHesapAdi'> {}

export const KASA_BANKA_HESAP_TIPLERI: Array<{ label: string; value: KasaBankaHesapTipi }> = [
    { label: 'Nakit Kasa', value: 'NakitKasa' },
    { label: 'Banka', value: 'Banka' }
];

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

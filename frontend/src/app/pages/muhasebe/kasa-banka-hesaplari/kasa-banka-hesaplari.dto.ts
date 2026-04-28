export type KasaBankaHesapTipi = 'NakitKasa' | 'Banka' | 'KrediKarti' | 'DovizHesabi';

export interface KasaBankaHesapModel {
    id?: number;
    tesisId?: number | null;
    tip: KasaBankaHesapTipi;
    kod: string;
    ad: string;
    muhasebeHesapPlaniId?: number | null;
    anaMuhasebeHesapKodu?: string | null;
    muhasebeHesapSiraNo?: number | null;
    paraBirimi?: string | null;
    valorGunSayisi: number;
    kartAdi?: string | null;
    kartNoMaskeli?: string | null;
    kartLimiti?: number | null;
    hesapKesimGunu?: number | null;
    sonOdemeGunu?: number | null;
    bagliBankaHesapId?: number | null;
    muhasebeTamKod?: string | null;
    muhasebeHesapAdi?: string | null;
    bankaAdi?: string | null;
    subeAdi?: string | null;
    hesapNo?: string | null;
    iban?: string | null;
    musteriNo?: string | null;
    hesapTuru?: string | null;
    sorumluKisi?: string | null;
    lokasyon?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateKasaBankaHesapRequest {
    tesisId?: number | null;
    tip: KasaBankaHesapTipi;
    kod?: string | null;
    ad: string;
    muhasebeHesapPlaniId?: number | null;
    paraBirimi?: string | null;
    valorGunSayisi?: number | null;
    kartAdi?: string | null;
    kartNoMaskeli?: string | null;
    kartLimiti?: number | null;
    hesapKesimGunu?: number | null;
    sonOdemeGunu?: number | null;
    bagliBankaHesapId?: number | null;
    bankaAdi?: string | null;
    subeAdi?: string | null;
    hesapNo?: string | null;
    iban?: string | null;
    musteriNo?: string | null;
    hesapTuru?: string | null;
    sorumluKisi?: string | null;
    lokasyon?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface UpdateKasaBankaHesapRequest extends CreateKasaBankaHesapRequest {}

export const KASA_BANKA_HESAP_TIPLERI: Array<{ label: string; value: KasaBankaHesapTipi }> = [
    { label: 'Kasa', value: 'NakitKasa' },
    { label: 'Banka', value: 'Banka' },
    { label: 'Kredi Karti', value: 'KrediKarti' },
    { label: 'Doviz Hesabi', value: 'DovizHesabi' }
];

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

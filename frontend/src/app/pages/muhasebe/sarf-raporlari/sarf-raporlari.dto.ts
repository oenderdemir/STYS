import { PagedResponseDto } from '../../../core/api';

export interface SarfRaporFilterModel {
    tesisId: number;
    baslangicTarihi?: string | null;
    bitisTarihi?: string | null;
    depoId?: number | null;
    tasinirKartId?: number | null;
    isletmeAlaniId?: number | null;
    odaId?: number | null;
    sarfNedeni?: string | null;
    durum?: string | null;
}

export interface SarfTuketimDetayRaporSatirModel {
    tarih: string;
    fisNo: string;
    sarfFisiId: number;
    sarfFisiSatirId: number;
    depoId: number;
    depoKod: string;
    depoAd: string;
    isletmeAlaniId?: number | null;
    isletmeAlaniAd?: string | null;
    odaId?: number | null;
    odaAd?: string | null;
    sarfNedeni?: string | null;
    tasinirKartId: number;
    stokKodu: string;
    malzemeAd: string;
    birim: string;
    miktar: number;
    lotNo?: string | null;
    seriNo?: string | null;
    durum: string;
    maliyetBirimFiyat?: number | null;
    toplamMaliyet?: number | null;
}

export interface SarfTuketimMalzemeOzetModel {
    tasinirKartId: number;
    stokKodu: string;
    malzemeAd: string;
    birim: string;
    toplamTuketimMiktari: number;
    sarfFisiSayisi: number;
    toplamTuketimMaliyeti: number;
}

export interface SarfTuketimKullanimYeriOzetModel {
    isletmeAlaniId?: number | null;
    isletmeAlaniAd?: string | null;
    odaId?: number | null;
    odaAd?: string | null;
    farkliMalzemeSayisi: number;
    toplamSarfSatiriSayisi: number;
    toplamMiktarOzeti: string;
    toplamTuketimMaliyeti: number;
}

export type SarfDetayPagedModel = PagedResponseDto<SarfTuketimDetayRaporSatirModel>;

export const SARF_RAPOR_DURUMLARI = [
    { label: 'Kesinleşti', value: 'Kesinlesti' },
    { label: 'İptal Edildi', value: 'IptalEdildi' },
    { label: 'Taslak', value: 'Taslak' }
];

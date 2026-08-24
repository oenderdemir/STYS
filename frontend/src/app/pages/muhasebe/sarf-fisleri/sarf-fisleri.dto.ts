export interface SarfFisiSatirModel {
    id?: number;
    sarfFisiId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    stokHareketId?: number | null;
    iptalStokHareketId?: number | null;
    takipTipi: string;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    seriNo?: string | null;
    miktar: number;
    aciklama?: string | null;
}

export interface SarfFisiModel {
    id?: number;
    tesisId: number;
    depoId: number;
    sarfTarihi: string;
    isletmeAlaniId?: number | null;
    isletmeAlaniAd?: string | null;
    birimAd?: string | null;
    odaId?: number | null;
    odaAd?: string | null;
    sarfNedeni?: string | null;
    durum: string;
    aciklama?: string | null;
    olusturanKullaniciId?: string | null;
    iptalTarihi?: string | null;
    iptalEdenKullaniciId?: string | null;
    iptalAciklamasi?: string | null;
    satirlar: SarfFisiSatirModel[];
}

export interface CreateSarfFisiRequest {
    depoId: number;
    sarfTarihi: string;
    isletmeAlaniId?: number | null;
    odaId?: number | null;
    sarfNedeni?: string | null;
    aciklama?: string | null;
}

export interface UpdateSarfFisiSatirlarRequest {
    satirlar: Array<{ id: number; miktar: number; stokLotId?: number | null; stokSeriId?: number | null; aciklama?: string | null }>;
}

export interface AddSarfFisiSatirRequest {
    tasinirKartId: number;
    miktar: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    aciklama?: string | null;
}

export interface IptalSarfFisiRequest {
    iptalAciklamasi?: string | null;
}

export interface SarfBirimSecenekModel {
    id: number;
    ad: string;
}

export interface SarfOdaSecenekModel {
    id: number;
    ad: string;
}

export const SARF_FISI_DURUMLARI: Array<{ label: string; value: string }> = [
    { label: 'Taslak', value: 'Taslak' },
    { label: 'Kesinleşti', value: 'Kesinlesti' },
    { label: 'İptal', value: 'Iptal' },
    { label: 'Geri Alındı', value: 'IptalEdildi' }
];

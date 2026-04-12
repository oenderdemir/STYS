export interface TesisSecenekModel {
    id: number;
    ad: string;
}

export interface RestoranModel {
    id?: number | null;
    tesisId: number;
    isletmeAlaniId?: number | null;
    isletmeAlaniAdi?: string | null;
    yoneticiUserIds?: string[] | null;
    garsonUserIds?: string[] | null;
    ad: string;
    aciklama?: string | null;
    aktifMi: boolean;
}

export interface CreateRestoranRequest {
    tesisId: number;
    isletmeAlaniId?: number | null;
    yoneticiUserIds?: string[] | null;
    garsonUserIds?: string[] | null;
    ad: string;
    aciklama?: string | null;
    aktifMi: boolean;
}

export interface UpdateRestoranRequest {
    tesisId: number;
    isletmeAlaniId?: number | null;
    yoneticiUserIds?: string[] | null;
    garsonUserIds?: string[] | null;
    ad: string;
    aciklama?: string | null;
    aktifMi: boolean;
}

export interface RestoranIsletmeAlaniSecenekModel {
    id: number;
    ad: string;
}

export interface RestoranMasaModel {
    id?: number | null;
    restoranId: number;
    masaNo: string;
    kapasite: number;
    durum: string;
    aktifMi: boolean;
}

export interface CreateRestoranMasaRequest {
    restoranId: number;
    masaNo: string;
    kapasite: number;
    durum: string;
    aktifMi: boolean;
}

export interface UpdateRestoranMasaRequest {
    restoranId: number;
    masaNo: string;
    kapasite: number;
    durum: string;
    aktifMi: boolean;
}

export interface RestoranMenuKategoriModel {
    id?: number | null;
    restoranId: number;
    ad: string;
    siraNo: number;
    aktifMi: boolean;
}

export interface CreateRestoranMenuKategoriRequest {
    restoranId: number;
    ad: string;
    siraNo: number;
    aktifMi: boolean;
}

export interface UpdateRestoranMenuKategoriRequest {
    restoranId: number;
    ad: string;
    siraNo: number;
    aktifMi: boolean;
}

export interface RestoranGlobalMenuKategoriModel {
    id: number;
    ad: string;
    siraNo: number;
    aktifMi: boolean;
    restoranSayisi: number;
}

export interface CreateRestoranGlobalMenuKategoriRequest {
    ad: string;
    siraNo: number;
    aktifMi: boolean;
}

export interface UpdateRestoranGlobalMenuKategoriRequest {
    ad: string;
    siraNo: number;
    aktifMi: boolean;
}

export interface RestoranKategoriAtamaBaglamModel {
    restoranId: number;
    globalKategoriler: RestoranGlobalMenuKategoriModel[];
    seciliGlobalKategoriIdleri: number[];
}

export interface SaveRestoranKategoriAtamaRequest {
    restoranId: number;
    seciliGlobalKategoriIdleri: number[];
}

export interface RestoranMenuUrunModel {
    id?: number | null;
    restoranMenuKategoriId: number;
    ad: string;
    aciklama?: string | null;
    fiyat: number;
    paraBirimi: string;
    hazirlamaSuresiDakika: number;
    aktifMi: boolean;
}

export interface CreateRestoranMenuUrunRequest {
    restoranMenuKategoriId: number;
    ad: string;
    aciklama?: string | null;
    fiyat: number;
    paraBirimi: string;
    hazirlamaSuresiDakika: number;
    aktifMi: boolean;
}

export interface UpdateRestoranMenuUrunRequest {
    restoranMenuKategoriId: number;
    ad: string;
    aciklama?: string | null;
    fiyat: number;
    paraBirimi: string;
    hazirlamaSuresiDakika: number;
    aktifMi: boolean;
}

export interface RestoranSiparisKalemiModel {
    id?: number | null;
    restoranMenuUrunId: number;
    urunAdiSnapshot: string;
    birimFiyat: number;
    miktar: number;
    satirToplam: number;
    notlar?: string | null;
}

export interface CreateRestoranSiparisKalemiRequest {
    restoranMenuUrunId: number;
    miktar: number;
    notlar?: string | null;
}

export interface RestoranSiparisModel {
    id?: number | null;
    restoranId: number;
    restoranMasaId?: number | null;
    siparisNo: string;
    siparisDurumu: string;
    toplamTutar: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string;
    odemeDurumu: string;
    notlar?: string | null;
    siparisTarihi: string;
    kalemler: RestoranSiparisKalemiModel[];
}

export interface CreateRestoranSiparisRequest {
    restoranId: number;
    restoranMasaId?: number | null;
    paraBirimi: string;
    notlar?: string | null;
    kalemler: CreateRestoranSiparisKalemiRequest[];
}

export interface UpdateRestoranSiparisRequest {
    restoranMasaId?: number | null;
    notlar?: string | null;
    kalemler: CreateRestoranSiparisKalemiRequest[];
}

export interface UpdateRestoranSiparisDurumRequest {
    siparisDurumu: string;
}

export interface RestoranOdemeModel {
    id?: number | null;
    restoranSiparisId: number;
    odemeTipi: string;
    tutar: number;
    paraBirimi: string;
    odemeTarihi: string;
    aciklama?: string | null;
    rezervasyonId?: number | null;
    rezervasyonOdemeId?: number | null;
    durum: string;
    islemReferansNo?: string | null;
}

export interface CreateNakitOdemeRequest {
    tutar: number;
    aciklama?: string | null;
}

export interface CreateKrediKartiOdemeRequest {
    tutar: number;
    aciklama?: string | null;
}

export interface CreateOdayaEkleOdemeRequest {
    rezervasyonId: number;
    tutar: number;
    aciklama?: string | null;
}

export interface RestoranSiparisOdemeOzetiModel {
    siparisToplami: number;
    odenenTutar: number;
    kalanTutar: number;
    odemeDurumu: string;
    odemeler: RestoranOdemeModel[];
}

export interface AktifRezervasyonAramaModel {
    rezervasyonId: number;
    tesisId: number;
    referansNo: string;
    misafirAdiSoyadi: string;
    odaNo: string;
    girisTarihi: string;
    cikisTarihi: string;
}

export const RESTORAN_MASA_DURUMLARI = [
    { label: 'Musait', value: 'Musait' },
    { label: 'Dolu', value: 'Dolu' },
    { label: 'Rezerve', value: 'Rezerve' },
    { label: 'Serviste', value: 'Serviste' },
    { label: 'Kapali', value: 'Kapali' }
] as const;

export const RESTORAN_SIPARIS_DURUMLARI = [
    { label: 'Taslak', value: 'Taslak' },
    { label: 'Hazirlaniyor', value: 'Hazirlaniyor' },
    { label: 'Hazir', value: 'Hazir' },
    { label: 'Serviste', value: 'Serviste' },
    { label: 'Tamamlandi', value: 'Tamamlandi' },
    { label: 'Iptal', value: 'Iptal' }
] as const;

export const RESTORAN_ODEME_DURUMLARI = [
    { label: 'Odenmedi', value: 'Odenmedi' },
    { label: 'Kismi Odendi', value: 'KismiOdendi' },
    { label: 'Odendi', value: 'Odendi' }
] as const;

export const RESTORAN_ODEME_TIPLERI = [
    { label: 'Nakit', value: 'Nakit' },
    { label: 'Kredi Karti', value: 'KrediKarti' },
    { label: 'Odaya Ekle', value: 'OdayaEkle' }
] as const;

export const PARA_BIRIMI_SECENEKLERI = [
    { label: 'TRY', value: 'TRY' },
    { label: 'USD', value: 'USD' },
    { label: 'EUR', value: 'EUR' }
] as const;

export function getMasaDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (durum) {
        case 'Musait':
            return 'success';
        case 'Serviste':
            return 'info';
        case 'Dolu':
            return 'warn';
        case 'Rezerve':
            return 'secondary';
        case 'Kapali':
            return 'danger';
        default:
            return 'secondary';
    }
}

export function getSiparisDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (durum) {
        case 'Tamamlandi':
            return 'success';
        case 'Serviste':
            return 'info';
        case 'Hazirlaniyor':
        case 'Hazir':
            return 'warn';
        case 'Iptal':
            return 'danger';
        default:
            return 'secondary';
    }
}

export function getOdemeDurumSeverity(durum: string): 'success' | 'warn' | 'secondary' {
    switch (durum) {
        case 'Odendi':
            return 'success';
        case 'KismiOdendi':
            return 'warn';
        default:
            return 'secondary';
    }
}

export interface KdvRaporFilterModel {
    tesisId?: number | null;
    baslangicTarihi: Date | null;
    bitisTarihi: Date | null;
    belgeYonu?: string | null;
    istisnalarDahilMi: boolean;
    tevkifatDahilMi: boolean;
}

export function createDefaultKdvRaporFilter(): KdvRaporFilterModel {
    const now = new Date();
    const baslangic = new Date(now.getFullYear(), now.getMonth(), 1);
    return {
        tesisId: null,
        baslangicTarihi: baslangic,
        bitisTarihi: now,
        belgeYonu: 'Hepsi',
        istisnalarDahilMi: true,
        tevkifatDahilMi: true
    };
}

export interface KdvOzetRaporModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    ozet: KdvOzetRaporOzetModel;
    oranOzetleri: KdvOranOzetModel[];
    istisnaOzetleri: KdvIstisnaOzetModel[];
}

export interface KdvOzetRaporOzetModel {
    toplamKayitSayisi: number;
    satisKayitSayisi: number;
    alisKayitSayisi: number;
    iadeKayitSayisi: number;
    satisMatrahToplam: number;
    hesaplananKdvToplam: number;
    alisMatrahToplam: number;
    indirilecekKdvToplam: number;
    satisIadeMatrahToplam: number;
    satisIadeKdvToplam: number;
    alisIadeMatrahToplam: number;
    alisIadeKdvToplam: number;
    istisnaMatrahToplam: number;
    tevkifatToplam: number;
    netKdv: number;
}

export interface KdvOranOzetModel {
    islemYonu: string;
    kdvOrani: number;
    hareketSayisi: number;
    matrah: number;
    kdvTutari: number;
}

export interface KdvIstisnaOzetModel {
    islemYonu: string;
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    hareketSayisi: number;
    matrah: number;
}

export interface TevkifatOzetRaporModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    satisTevkifatToplam: number;
    alisTevkifatToplam: number;
    netTevkifat: number;
    toplamKayitSayisi: number;
    oranOzetleri: TevkifatOranOzetModel[];
}

export interface TevkifatOranOzetModel {
    islemYonu: string;
    tevkifatPay: number;
    tevkifatPayda: number;
    hareketSayisi: number;
    matrah: number;
    tevkifatTutari: number;
}

export const DONEM_SECENEKLERI: Array<{ label: string; value: number }> = [
    { label: 'Ocak', value: 1 },
    { label: 'Şubat', value: 2 },
    { label: 'Mart', value: 3 },
    { label: 'Nisan', value: 4 },
    { label: 'Mayıs', value: 5 },
    { label: 'Haziran', value: 6 },
    { label: 'Temmuz', value: 7 },
    { label: 'Ağustos', value: 8 },
    { label: 'Eylül', value: 9 },
    { label: 'Ekim', value: 10 },
    { label: 'Kasım', value: 11 },
    { label: 'Aralık', value: 12 }
];

export function getDonemLabel(donem: number): string {
    const found = DONEM_SECENEKLERI.find(d => d.value === donem);
    return found?.label ?? `${donem}. Dönem`;
}

export function getMaliYilSecenekleri(): Array<{ label: string; value: number }> {
    const currentYear = new Date().getFullYear();
    const years: Array<{ label: string; value: number }> = [];
    for (let y = currentYear - 3; y <= currentYear + 1; y++) {
        years.push({ label: y.toString(), value: y });
    }
    return years;
}

export const BELGE_YONU_SECENEKLERI: Array<{ label: string; value: string }> = [
    { label: 'Hepsi', value: 'Hepsi' },
    { label: 'Satış', value: 'Satis' },
    { label: 'Alış', value: 'Alis' },
    { label: 'İade', value: 'Iade' }
];

export const UYARI_KODU_LABELLERI: Record<string, string> = {
    'MUHASEBE_FISI_EKSIK': 'Eksik Muhasebe Fişi',
    'KDV_TUTARI_EKSIK': 'KDV Tutarı Eksik',
    'ISTISNA_KODU_EKSIK': 'İstisna Kodu Eksik',
    'TEVKIFATLI_HAREKET_VAR': 'Tevkifatlı Hareket Var'
};

export const UYARI_KODU_ICONS: Record<string, string> = {
    'MUHASEBE_FISI_EKSIK': 'pi pi-file-edit',
    'KDV_TUTARI_EKSIK': 'pi pi-exclamation-circle',
    'ISTISNA_KODU_EKSIK': 'pi pi-tag',
    'TEVKIFATLI_HAREKET_VAR': 'pi pi-info-circle'
};

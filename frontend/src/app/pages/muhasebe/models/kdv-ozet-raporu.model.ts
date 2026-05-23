export interface KdvOzetRaporFilterModel {
    maliYil?: number | null;
    donem?: number | null;
    baslangicTarihi?: string | null;
    bitisTarihi?: string | null;
    tesisId?: number | null;
    depoId?: number | null;
    tasinirKartId?: number | null;
    hareketTipi?: string | null;
    kdvUygulamaTipi?: number | null;
    kdvIstisnaTanimId?: number | null;
    kdvIstisnaKodu?: string | null;
    musFisDurumu?: string | null;
}

export function createDefaultKdvOzetRaporFilter(): KdvOzetRaporFilterModel {
    const now = new Date();
    return {
        maliYil: now.getFullYear(),
        donem: now.getMonth() + 1, // 1-12
        baslangicTarihi: null,
        bitisTarihi: null,
        tesisId: null,
        depoId: null,
        tasinirKartId: null,
        hareketTipi: null,
        kdvUygulamaTipi: null,
        kdvIstisnaTanimId: null,
        kdvIstisnaKodu: null,
        musFisDurumu: null
    };
}

export interface KdvOzetRaporModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    ozet: KdvOzetRaporOzetModel;
    uygulamaTipiOzetleri: KdvUygulamaTipiOzetModel[];
    istisnaKoduOzetleri: KdvIstisnaKoduOzetModel[];
    uyarilar: KdvOzetRaporUyariModel[];
}

export interface KdvOzetRaporOzetModel {
    donemLabel: string;
    // Satış (Hesaplanan KDV)
    satisHareketSayisi: number;
    satisMatrahi: number;
    hesaplananKdvTutari: number;
    // Alış (İndirilecek KDV)
    alisHareketSayisi: number;
    alisMatrahi: number;
    indirilecekKdvTutari: number;
    // Net KDV
    netKdv: number;
    // İstisna / Kapsam Dışı
    istisnaMatrahi: number;
    tamIstisnaMatrahi: number;
    kismiIstisnaMatrahi: number;
    kapsamDisiMatrah: number;
    // Genel
    toplamKayitSayisi: number;
    fisiOlanSayisi: number;
    fisiOlmayanSayisi: number;
}

export interface KdvUygulamaTipiOzetModel {
    kdvUygulamaTipi: number;
    kdvUygulamaTipiAd: string;
    hareketSayisi: number;
    matrah: number;
    kdvTutari: number;
}

export interface KdvIstisnaKoduOzetModel {
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    hareketSayisi: number;
    matrah: number;
}

export interface KdvOzetRaporUyariModel {
    uyariKodu: string;
    uyariMesaji: string;
    etkilenenKayitSayisi: number;
    severity?: string | null;
    route?: string | null;
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

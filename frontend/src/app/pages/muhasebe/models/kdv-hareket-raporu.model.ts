export interface KdvHareketRaporFilterModel {
    baslangicTarihi: string;
    bitisTarihi: string;
    tesisId?: number | null;
    depoId?: number | null;
    tasinirKartId?: number | null;
    hareketTipi?: string | null;
    kdvUygulamaTipi?: number | null;
    kdvIstisnaTanimId?: number | null;
    kdvIstisnaKodu?: string | null;
    musFisDurumu?: string | null;
}

export function createDefaultKdvHareketRaporFilter(): KdvHareketRaporFilterModel {
    const today = new Date();
    const startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    return {
        baslangicTarihi: startOfMonth.toISOString(),
        bitisTarihi: today.toISOString(),
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

export interface KdvHareketRaporModel {
    satirlar: KdvHareketRaporSatirModel[];
    ozet: KdvHareketRaporOzetModel;
    toplamKayitSayisi: number;
}

export interface KdvHareketRaporSatirModel {
    id: number;
    hareketTarihi: string;
    hareketTipi: string;
    depoAdi: string;
    tasinirKod: string;
    tasinirAd: string;
    miktar: number;
    birimFiyat: number;
    tutar: number;
    kdvUygulamaTipi: number;
    kdvUygulamaTipiAd: string;
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    kdvOrani: number;
    kdvTutari: number;
    kdvliTutar: number;
    musFisId?: number | null;
    musFisNo?: string | null;
    musFisDurumu?: string | null;
    belgeNo?: string | null;
    aciklama?: string | null;
}

export interface KdvHareketRaporOzetModel {
    toplamKayitSayisi: number;
    kdvliSayisi: number;
    istisnaliSayisi: number;
    kdvKapsamDisiSayisi: number;
    tevkifatliSayisi: number;
    fisiOlanSayisi: number;
    fisiOlmayanSayisi: number;
    toplamKdvTutari: number;
    toplamTutar: number;
}

export const KDV_UYGULAMA_TIPI_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Tümü', value: null },
    { label: 'KDV\'li', value: 1 },
    { label: 'Tam İstisna', value: 2 },
    { label: 'Kısmi İstisna', value: 3 },
    { label: 'KDV Kapsam Dışı', value: 4 },
    { label: 'Tevkifatlı', value: 5 }
];

export const MUS_FIS_DURUMU_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Fişi Olan', value: 'FisiOlan' },
    { label: 'Fişi Olmayan', value: 'FisiOlmayan' }
];

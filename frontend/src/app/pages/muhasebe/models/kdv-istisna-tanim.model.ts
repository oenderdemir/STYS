export type KdvUygulamaTipi = 1 | 2 | 3 | 4 | 5;

export const KDV_UYGULAMA_TIPI_LABELS: Record<KdvUygulamaTipi, string> = {
    1: "KDV'li",
    2: 'Tam İstisna',
    3: 'Kısmi İstisna',
    4: 'KDV Kapsam Dışı',
    5: 'Tevkifatlı'
};

export const KDV_UYGULAMA_TIPI_SECENEKLERI: Array<{ label: string; value: KdvUygulamaTipi }> =
    Object.entries(KDV_UYGULAMA_TIPI_LABELS).map(([key, label]) => ({
        label,
        value: Number(key) as KdvUygulamaTipi
    }));

export const ISTISNA_SECENEKLERI: Array<{ label: string; value: KdvUygulamaTipi }> =
    KDV_UYGULAMA_TIPI_SECENEKLERI.filter(s => s.value !== 1);

export interface KdvIstisnaTanimDto {
    id: number;
    kod: string;
    ad: string;
    aciklama: string | null;
    uygulamaTipi: KdvUygulamaTipi;
    satisIslemlerindeKullanilirMi: boolean;
    alisIslemlerindeKullanilirMi: boolean;
    yuklenilenKdvIndirilebilirMi: boolean;
    iadeHakkiVarMi: boolean;
    eBelgeKoduZorunluMu: boolean;
    aktifMi: boolean;
    gecerlilikBaslangicTarihi: string | null;
    gecerlilikBitisTarihi: string | null;
}

export interface CreateKdvIstisnaTanimRequest {
    kod: string;
    ad: string;
    aciklama: string | null;
    uygulamaTipi: KdvUygulamaTipi;
    satisIslemlerindeKullanilirMi: boolean;
    alisIslemlerindeKullanilirMi: boolean;
    yuklenilenKdvIndirilebilirMi: boolean;
    iadeHakkiVarMi: boolean;
    eBelgeKoduZorunluMu: boolean;
    aktifMi: boolean;
    gecerlilikBaslangicTarihi: string | null;
    gecerlilikBitisTarihi: string | null;
}

export interface UpdateKdvIstisnaTanimRequest {
    kod: string;
    ad: string;
    aciklama: string | null;
    uygulamaTipi: KdvUygulamaTipi;
    satisIslemlerindeKullanilirMi: boolean;
    alisIslemlerindeKullanilirMi: boolean;
    yuklenilenKdvIndirilebilirMi: boolean;
    iadeHakkiVarMi: boolean;
    eBelgeKoduZorunluMu: boolean;
    aktifMi: boolean;
    gecerlilikBaslangicTarihi: string | null;
    gecerlilikBitisTarihi: string | null;
}

export interface KdvIstisnaTanimFilterDto {
    kod: string | null;
    ad: string | null;
    uygulamaTipi: KdvUygulamaTipi | null;
    aktifMi: boolean | null;
    satisIslemlerindeKullanilirMi: boolean | null;
    alisIslemlerindeKullanilirMi: boolean | null;
}

export function createDefaultKdvIstisnaTanimFilter(): KdvIstisnaTanimFilterDto {
    return {
        kod: null,
        ad: null,
        uygulamaTipi: null,
        aktifMi: null,
        satisIslemlerindeKullanilirMi: null,
        alisIslemlerindeKullanilirMi: null
    };
}

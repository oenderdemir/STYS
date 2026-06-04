export interface CariKartModel {
    id?: number;
    tesisId?: number | null;
    cariTipi: string;
    cariKodu: string;
    muhasebeHesapPlaniId?: number | null;
    anaMuhasebeHesapKodu?: string | null;
    muhasebeHesapSiraNo?: number | null;
    unvanAdSoyad: string;
    vergiNoTckn?: string | null;
    vergiDairesi?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    adres?: string | null;
    il?: string | null;
    ilce?: string | null;
    aktifMi: boolean;
    eFaturaMukellefiMi: boolean;
    eArsivKapsamindaMi: boolean;
    aciklama?: string | null;
    acilisBakiyeTarihi?: string | null;
    acilisBakiyeTutari?: number | null;
    acilisBakiyeYonu?: string | null;
    bankaAdi?: string | null;
    iban?: string | null;
    acilisBakiyeDuzeltilebilirMi?: boolean;
    bankaHesaplari: CariKartBankaHesabiModel[];
    yetkiliKisiler: CariKartYetkiliKisiModel[];
}

export interface CreateCariKartRequest {
    tesisId?: number | null;
    cariTipi: string;
    cariKodu?: string | null;
    unvanAdSoyad: string;
    vergiNoTckn?: string | null;
    vergiDairesi?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    adres?: string | null;
    il?: string | null;
    ilce?: string | null;
    aktifMi: boolean;
    eFaturaMukellefiMi: boolean;
    eArsivKapsamindaMi: boolean;
    aciklama?: string | null;
    acilisBakiyeTarihi?: string | null;
    acilisBakiyeTutari?: number | null;
    acilisBakiyeYonu?: string | null;
    bankaAdi?: string | null;
    iban?: string | null;
    bankaHesaplari: CariKartBankaHesabiModel[];
    yetkiliKisiler: CariKartYetkiliKisiModel[];
}

export interface UpdateCariKartRequest extends CreateCariKartRequest {}

export interface CariKartAcilisBakiyesiDuzeltRequest {
    yeniTutar: number;
    yeniYonu?: string | null;
    duzeltmeTarihi?: string | Date | null;
}

export interface CariKartYetkiliKisiModel {
    id?: number | null;
    cariKartId?: number | null;
    adSoyad: string;
    gorevUnvan?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aciklama?: string | null;
}

export interface CariKartBankaHesabiModel {
    id?: number | null;
    cariKartId?: number | null;
    bankaAdi?: string | null;
    subeAdi?: string | null;
    hesapNo?: string | null;
    iban?: string | null;
    aciklama?: string | null;
}

export interface CariBakiyeModel {
    cariKartId: number;
    cariKodu: string;
    unvanAdSoyad: string;
    toplamBorc: number;
    toplamAlacak: number;
    bakiye: number;
    paraBirimi: string;
}

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

export const CARI_KART_TIPLERI = {
    Musteri: 'Musteri',
    Tedarikci: 'Tedarikci',
    KurumsalMusteri: 'KurumsalMusteri',
    Personel: 'Personel',
    Diger: 'Diger'
} as const;

export const CARI_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Musteri', value: CARI_KART_TIPLERI.Musteri },
    { label: 'Tedarikci', value: CARI_KART_TIPLERI.Tedarikci },
    { label: 'Kurumsal Musteri', value: CARI_KART_TIPLERI.KurumsalMusteri },
    { label: 'Personel', value: CARI_KART_TIPLERI.Personel },
    { label: 'Diger', value: CARI_KART_TIPLERI.Diger }
];

export const SATIS_CARI_TIPLERI = [
    CARI_KART_TIPLERI.Musteri,
    CARI_KART_TIPLERI.KurumsalMusteri
] as const;

export const ALIS_CARI_TIPLERI = [
    CARI_KART_TIPLERI.Tedarikci
] as const;


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
}

export interface UpdateCariKartRequest extends CreateCariKartRequest {}

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

export const CARI_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Musteri', value: 'Musteri' },
    { label: 'Tedarikci', value: 'Tedarikci' },
    { label: 'Kurumsal Musteri', value: 'KurumsalMusteri' },
    { label: 'Personel', value: 'Personel' },
    { label: 'Diger', value: 'Diger' }
];


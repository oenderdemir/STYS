export interface KantinModel {
    id?: number;
    tesisId: number;
    depoId: number;
    perakendeCariKartId?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
    aciklama?: string | null;
    depoKod?: string | null;
    depoAd?: string | null;
    perakendeCariKartAd?: string | null;
}

export interface KantinSatisNoktasiModel {
    id?: number;
    kantinId: number;
    kod: string;
    ad: string;
    varsayilanNakitKasaId?: number | null;
    varsayilanPosHesapId?: number | null;
    varsayilanMi: boolean;
    aktifMi: boolean;
    aciklama?: string | null;
    varsayilanNakitKasaAd?: string | null;
    varsayilanPosHesapAd?: string | null;
}

export interface KantinUrunModel {
    id?: number;
    kantinId: number;
    tasinirKartId: number;
    siraNo?: number | null;
    barkod?: string | null;
    satisFiyati: number;
    aktifMi: boolean;
    aciklama?: string | null;
    stokKodu?: string | null;
    urunAdi?: string | null;
    birim?: string | null;
    kdvOrani: number;
    mevcutStok: number;
    takipTipi?: string | null;
}

export interface KantinDepoOption {
    id: number;
    kod: string;
    ad: string;
}

export interface KantinKasaOption {
    id: number;
    kod: string;
    ad: string;
}

export interface KantinCariKartOption {
    id: number;
    cariKodu: string;
    unvanAdSoyad: string;
}

export interface KantinOdemeHesapOption {
    id: number;
    kod: string;
    ad: string;
    tip: string;
}

export interface KantinTasinirKartOption {
    id: number;
    stokKodu: string;
    ad: string;
    birim: string;
    kdvOrani: number;
}

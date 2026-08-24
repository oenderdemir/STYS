export interface KantinModel {
    id?: number;
    tesisId: number;
    depoId: number;
    varsayilanNakitKasaId?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
    aciklama?: string | null;
    depoKod?: string | null;
    depoAd?: string | null;
    varsayilanNakitKasaAd?: string | null;
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

export interface KantinTasinirKartOption {
    id: number;
    stokKodu: string;
    ad: string;
    birim: string;
    kdvOrani: number;
}

export interface KantinSatisModel {
    id?: number;
    tesisId: number;
    kantinId: number;
    satisTarihi: string;
    durum: string;
    toplamTutar: number;
    matrahToplami: number;
    kdvToplami: number;
    aciklama?: string | null;
    kesinlesmeTarihi?: string | null;
    kantinKod?: string | null;
    kantinAd?: string | null;
    odemeOzeti?: string | null;
    satirlar: KantinSatisSatirModel[];
    odemeler: KantinSatisOdemeModel[];
}

export interface KantinSatisSatirModel {
    id?: number;
    kantinSatisId: number;
    kantinUrunId: number;
    tasinirKartId: number;
    miktar: number;
    birimSatisFiyati: number;
    kdvOrani: number;
    matrah: number;
    kdvTutari: number;
    toplamTutar: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    stokHareketId?: number | null;
    barkod?: string | null;
    stokKodu: string;
    urunAdi: string;
    birim: string;
    takipTipi?: string | null;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    seriNo?: string | null;
}

export interface KantinSatisOdemeModel {
    id?: number;
    kantinSatisId: number;
    odemeYontemi: string;
    kasaBankaHesapId?: number | null;
    tutar: number;
    hesapKodSnapshot?: string | null;
    hesapAdSnapshot?: string | null;
}

export interface CreateKantinSatisRequest {
    kantinId: number;
    satisTarihi?: string | null;
    aciklama?: string | null;
}

export interface AddKantinSatisSatirRequest {
    kantinUrunId: number;
    miktar: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
}

export interface AddKantinSatisOdemeRequest {
    odemeYontemi: string;
    kasaBankaHesapId?: number | null;
    tutar: number;
}

export interface KantinSatisBarkodUrunModel {
    kantinUrunId: number;
    tasinirKartId: number;
    stokKodu: string;
    urunAdi: string;
    birim: string;
    barkod?: string | null;
    satisFiyati: number;
    kdvOrani: number;
    mevcutStok: number;
    takipTipi: string;
}

export const KANTIN_ODEME_YONTEMLERI = {
    Nakit: 'Nakit',
    KrediKarti: 'KrediKarti'
} as const;

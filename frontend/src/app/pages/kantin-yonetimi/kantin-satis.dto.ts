export interface KantinSatisModel {
    id?: number;
    tesisId: number;
    kantinId: number;
    satisNoktasiId: number;
    satisTarihi: string;
    durum: string;
    toplamTutar: number;
    matrahToplami: number;
    kdvToplami: number;
    aciklama?: string | null;
    kesinlesmeTarihi?: string | null;
    muhasebeFisId?: number | null;
    muhasebeFisNo?: string | null;
    muhasebeFisDurumu?: string | null;
    muhasebeFisOlusturmaTarihi?: string | null;
    iptalTarihi?: string | null;
    iptalAciklamasi?: string | null;
    kantinKod?: string | null;
    kantinAd?: string | null;
    satisNoktasiKod?: string | null;
    satisNoktasiAd?: string | null;
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
    tahsilatOdemeBelgesiId?: number | null;
    tutar: number;
    hesapKodSnapshot?: string | null;
    hesapAdSnapshot?: string | null;
    tahsilatBelgeNo?: string | null;
    posBeklenenValorTarihi?: string | null;
    posValorDurumu?: string | null;
}

export interface CreateKantinSatisRequest {
    kantinId: number;
    satisNoktasiId: number;
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

export interface CancelKantinSatisRequest {
    aciklama: string;
}

export interface KantinSatisIadeSatirModel {
    id?: number;
    kantinSatisIadeId: number;
    kantinSatisSatirId: number;
    miktar: number;
    tasinirKartId: number;
    stokKodu: string;
    urunAdi: string;
    birim: string;
    takipTipi: string;
    lotNo?: string | null;
    seriNo?: string | null;
    birimSatisFiyati: number;
    kdvOrani: number;
    maliyetBirimFiyat?: number | null;
    maliyetTutari?: number | null;
    stokHareketId?: number | null;
    satilanMiktar: number;
    oncekiIadeMiktari: number;
    kalanMiktar: number;
}

export interface KantinSatisIadeModel {
    id?: number;
    tesisId: number;
    kantinSatisId: number;
    iadeTarihi: string;
    durum: string;
    aciklama?: string | null;
    olusturanKullaniciId?: string | null;
    kesinlesmeTarihi?: string | null;
    finansalIadeDurumu: string;
    satirlar: KantinSatisIadeSatirModel[];
}

export interface CreateKantinSatisIadeSatirRequest {
    kantinSatisSatirId: number;
    miktar: number;
}

export interface CreateKantinSatisIadeRequest {
    kantinSatisId: number;
    aciklama?: string | null;
    satirlar: CreateKantinSatisIadeSatirRequest[];
}

export interface KantinSatisIadeOzetModel {
    kantinSatisSatirId: number;
    satilanMiktar: number;
    oncekiIadeMiktari: number;
    kalanMiktar: number;
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

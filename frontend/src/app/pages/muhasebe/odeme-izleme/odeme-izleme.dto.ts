export const ODEME_GUVEN_SEVIYELERI = {
    Kesin: 'Kesin',
    YuksekOlasilik: 'YuksekOlasilik',
    IncelenmesiGereken: 'IncelenmesiGereken',
    EslesmeYok: 'EslesmeYok'
} as const;

export const UYARI_LABELLARI: Record<string, string> = {
    OdemeVarFisYok: 'Ödeme var, muhasebe fişi yok',
    PosVarValorYok: 'POS ödemesi var, valör kaydı yok',
    KapatmaHedefiVarAmaKapanmamis: 'Borç kapatma hedefi var ama kapanmamış',
    IptalAmaKapamaGeriAlinmamis: 'İptal edilmiş ama kapama geri alınmamış',
    AyniTutarAyniTarihFarkliCari: 'Aynı tutar/tarih/hesapla başka cariye kayıt var',
    MukerrerBelgeNo: 'Mükerrer belge numarası',
    ParaBirimiTutarsizligi: 'Para birimi POS kaydıyla uyuşmuyor',
    FarkliMuhasebeDonemineDusme: 'Farklı muhasebe dönemine düşmüş'
};

export const GUVEN_SEVIYESI_LABELLARI: Record<string, string> = {
    Kesin: 'Kesin',
    YuksekOlasilik: 'Yüksek Olasılık',
    IncelenmesiGereken: 'İncelenmesi Gereken',
    EslesmeYok: 'Eşleşme Yok'
};

export interface OdemeAramaFilterModel {
    tesisId?: number | null;
    belgeNo?: string | null;
    cariKartId?: number | null;
    cariAramaMetni?: string | null;
    tarihBaslangic?: string | null;
    tarihBitis?: string | null;
    tutarMin?: number | null;
    tutarMax?: number | null;
    paraBirimi?: string | null;
    odemeYontemi?: string | null;
    belgeTipi?: string | null;
    kasaBankaHesapId?: number | null;
    durum?: string | null;
    valorDurumu?: string | null;
    sadeceFissizOlanlar?: boolean | null;
}

export interface OdemeAramaSatiriModel {
    id: number;
    belgeNo: string;
    belgeTarihi: string;
    belgeTipi: string;
    durum: string;
    tutar: number;
    paraBirimi: string;
    odemeYontemi: string;
    cariKartId: number;
    cariKodu: string;
    cariUnvan: string;
    kasaBankaHesapAdi?: string | null;
    muhasebeFisId?: number | null;
    uyariSayisi: number;
}

export interface OdemeUyariModel {
    uyariTipi: string;
    guvenSeviyesi: string;
    aciklama: string;
    iliskiliBelgeId?: number | null;
}

export interface OdemeDetayModel {
    id: number;
    belgeNo: string;
    belgeTarihi: string;
    belgeTipi: string;
    durum: string;
    tutar: number;
    paraBirimi: string;
    odemeYontemi: string;
    aciklama?: string | null;
    cariKartId: number;
    cariKodu: string;
    cariUnvan: string;
    tesisId?: number | null;
    tesisAdi?: string | null;
    kasaBankaHesapId?: number | null;
    kasaBankaHesapAdi?: string | null;
    kasaBankaHesapTipi?: string | null;
    bankaAdi?: string | null;
    ibanMaskeli?: string | null;
    muhasebeHesapKodu?: string | null;
    muhasebeFisId?: number | null;
    muhasebeFisNo?: string | null;
    muhasebeFisTarihi?: string | null;
    muhasebeFisDurumu?: string | null;
    posTahsilatValorId?: number | null;
    posValorDurumu?: string | null;
    posBeklenenValorTarihi?: string | null;
    posNetTutar?: number | null;
    kapatilacakCariHareketId?: number | null;
    kapatildiMi: boolean;
    rezervasyonId?: number | null;
    rezervasyonReferansNo?: string | null;
    olusturanKullanici?: string | null;
    olusturmaTarihi?: string | null;
    degistirenKullanici?: string | null;
    degisiklikTarihi?: string | null;
    bakiyeyeDahilMi: boolean;
    /** TamamenDahil | KismenDahil | DahilDegil */
    bakiyeyeDahilEdilmeDurumu: string;
    bakiyeyeDahilEdilmemeNedenKodlari: string[];
    bakiyeyeDahilEdilmemeAciklamalari: string[];
    etkiledigiCariVeyaBorc?: string | null;
    etkiledigiTutar?: number | null;
    uyarilar: OdemeUyariModel[];
}

export interface CariHareketDokumFilterModel {
    cariKartId: number;
    tarihBaslangic?: string | null;
    tarihBitis?: string | null;
}

export interface CariHareketDokumSatiriModel {
    id: number;
    hareketTarihi: string;
    belgeTuru: string;
    belgeNo?: string | null;
    aciklama?: string | null;
    borcTutari: number;
    alacakTutari: number;
    kalanTutar: number;
    durum: string;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    kapandiMi: boolean;
    paraBirimi: string;
    kumulatifBakiye: number;
    hesaplamaDisiMi: boolean;
}

/** Para birimi bazinda ayristirilmis cari bakiye ozeti - farkli para birimleri BIRLESTIRILMEZ. */
export interface CariBakiyeParaBirimiOzetiModel {
    paraBirimi: string;
    toplamBorc: number;
    toplamAlacak: number;
    iptalEdilmisTutar: number;
    normalAktarilmayiBekleyenPos: number;
    mutabakatBekleyenPos: number;
    hataliPos: number;
    aktarimSurecindekiPos: number;
    aciklananKalanBakiye: number;
}

export interface CariHareketDokumModel {
    cariKartId: number;
    cariUnvan: string;
    acilisBakiyeTutari: number;
    acilisBakiyeYonu?: string | null;
    hareketler: CariHareketDokumSatiriModel[];
    paraBirimiOzetleri: CariBakiyeParaBirimiOzetiModel[];
}

export interface BeyanEdilenOdemeKarsilastirmaFilterModel {
    tesisId?: number | null;
    tarih: string;
    tarihToleransGun?: number;
    tutar: number;
    paraBirimi: string;
    odemeYontemi?: string | null;
    kasaBankaHesapId?: number | null;
    belgeNoTahmini?: string | null;
    cariKartId?: number | null;
}

export interface BeyanEdilenOdemeEslesmeModel {
    odemeId: number;
    belgeNo: string;
    belgeTarihi: string;
    tutar: number;
    paraBirimi: string;
    odemeYontemi: string;
    cariUnvan: string;
    guvenSeviyesi: string;
    gerekce: string;
    eslesenAlanlar: string[];
    uyusmayanAlanlar: string[];
    /** Toleransli tarih eslesmesi "birebir" olarak GOSTERILMEZ. */
    tarihBirebirMi: boolean;
    tarihFarkiGun: number;
}

export const ODEME_YONTEMI_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Nakit', value: 'Nakit' },
    { label: 'Kredi Kartı', value: 'KrediKarti' },
    { label: 'Havale/EFT', value: 'HavaleEft' },
    { label: 'Odaya Ekle', value: 'OdayaEkle' },
    { label: 'Mahsup', value: 'Mahsup' }
];

export const DURUM_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Aktif', value: 'Aktif' },
    { label: 'İptal', value: 'Iptal' }
];

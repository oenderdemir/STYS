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
    muhasebeFisNo?: string | null;
    rezervasyonReferansNo?: string | null;
    olusturulmaBaslangic?: string | null;
    olusturulmaBitis?: string | null;
    olusturanKullanici?: string | null;
    maliYil?: number | null;
    donem?: number | null;
    iban?: string | null;
    sadeceIptalEdilmisOlanlar?: boolean | null;
}

/** Capraz-kaynak arastirmada bir adayin uretildigi kaynak. */
export const ODEME_ADAY_KAYNAKLARI: Record<string, string> = {
    TahsilatOdemeBelgesi: 'Ödeme/Tahsilat Belgesi',
    CariHareket: 'Cari Hareket',
    PosTahsilatValor: 'POS Valör',
    KasaHareket: 'Kasa Hareketi',
    BankaHareket: 'Banka Hareketi',
    MuhasebeFis: 'Muhasebe Fişi'
};

export const KOPUKLUK_LABELLARI: Record<string, string> = {
    OdemeBaglantisiOlmayanMuhasebeFisi: 'Ödeme bağlantısı olmayan muhasebe fişi',
    OdemeBelgesiOlmayanCariHareket: 'Ödeme belgesi olmayan cari hareket',
    MuhasebeFisiOlmayanOdemeBelgesi: 'Muhasebe fişi olmayan ödeme belgesi',
    CariHareketEtkisiOlmayanOdemeBelgesi: 'Cari hareket etkisi olmayan ödeme belgesi',
    ValorKaydiOlmayanPosTahsilati: 'Valör kaydı olmayan POS tahsilatı',
    HedefBankaHesabiOlmayanValor: 'Hedef banka hesabı olmayan valör',
    OdemeBelgesiOlmayanKasaHareketi: 'Ödeme belgesi olmayan kasa hareketi',
    OdemeBelgesiOlmayanBankaHareketi: 'Ödeme belgesi olmayan banka hareketi',
    SoftDeleteIliskiNedeniyleGorunmeyen: 'Soft-delete ilişki nedeniyle görünmeyen kayıt'
};

export interface OdemeCaprazAramaFilterModel {
    tesisId?: number | null;
    tarihBaslangic?: string | null;
    tarihBitis?: string | null;
    tutarMin?: number | null;
    tutarMax?: number | null;
    cariKartId?: number | null;
    kopuklukTipi?: string | null;
    sadeceKopukOlanlar?: boolean;
    paraBirimi?: string | null;
    belgeNo?: string | null;
    muhasebeFisNo?: string | null;
    kasaBankaHesapId?: number | null;
    maliYil?: number | null;
    donem?: number | null;
    sadeceIptalEdilmisOlanlar?: boolean | null;
}

/** En az bir alan dolu olmadan capraz arama backend tarafindan REDDEDILIR (400).
 * Frontend bu listeyi kullanarak istegi gondermeden ONCE kullaniciyi uyarir. */
export function caprazAramaDaralticiAlanVarMi(f: OdemeCaprazAramaFilterModel): boolean {
    return !!(
        f.cariKartId ||
        (f.belgeNo && f.belgeNo.trim()) ||
        (f.muhasebeFisNo && f.muhasebeFisNo.trim()) ||
        f.kasaBankaHesapId ||
        (f.tarihBaslangic && f.tarihBitis) ||
        f.tutarMin != null ||
        f.tutarMax != null
    );
}

export interface OdemeAdayiModel {
    kaynak: string;
    kaynakId: number;
    tekillestirmeAnahtari: string;
    bulunduguKaynaklar: string[];
    tahsilatOdemeBelgesiId?: number | null;
    cariHareketId?: number | null;
    posTahsilatValorId?: number | null;
    muhasebeFisId?: number | null;
    kasaBankaHesapId?: number | null;
    belgeNo?: string | null;
    tarih?: string | null;
    tutar?: number | null;
    paraBirimi?: string | null;
    cariKartId?: number | null;
    cariUnvan?: string | null;
    tesisId?: number | null;
    aciklama?: string | null;
    kopuklukKodlari: string[];
    kopuklukAciklamalari: string[];
    /** Bu aday hicbir odeme/tahsilat belgesine BAGLI degil - KANITLANMAMIS, yalnizca filtre
     * kriterleriyle (tarih/tutar/cari/hesap) daraltilmis bagimsiz bir kayittir. */
    bagimsizKayitMi: boolean;
    /** Kesin | YuksekOlasilik | IncelenmesiGereken */
    guvenSeviyesi: string;
    guvenGerekcesi: string;
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
    /** true ise bagli kasa/banka hesabinin adi/IBAN'i/kodu mevcut yetki kapsamiyla
     * uyusmadigi icin GOSTERILMEMISTIR (asagidaki alanlar bilerek bos birakilmistir). */
    bagliHesapErisimKisitliMi: boolean;
    /** true ise bagli muhasebe fisinin no/tarih/durum bilgisi mevcut yetki kapsamiyla
     * uyusmadigi icin GOSTERILMEMISTIR. */
    bagliFisErisimKisitliMi: boolean;
    erisimKisitiNedenKodlari: string[];
}

export const ERISIM_KISITI_LABELLARI: Partial<Record<string, string>> = {
    YETKI_KAPSAMI_DISINDA_HESAP_BAGLANTISI: 'Bağlı hesap mevcut yetki kapsamınızla uyuşmuyor.',
    YETKI_KAPSAMI_DISINDA_FIS_BAGLANTISI: 'Bağlı muhasebe fişi mevcut yetki kapsamınızla uyuşmuyor.',
    TESIS_UYUSMAZLIGI: 'Bağlı kayıt farklı bir tesise ait.',
    KURUM_UYUSMAZLIGI: 'Bağlı kayıt farklı bir kuruma ait.'
};

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

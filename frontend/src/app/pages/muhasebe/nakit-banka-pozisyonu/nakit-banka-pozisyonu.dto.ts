export interface NakitBankaPozisyonuFilterModel {
    tesisId?: number | null;
    raporTarihi?: string | null;
    maliYil?: number | null;
    donem?: number | null;
    hesapTuru?: string | null;
    bankaHesapId?: number | null;
    paraBirimi?: string | null;
    /** Yalnizca valor takvimi/gun detay sorgularini etkiler - ozet/hesap toplamlarini ETKILEMEZ. */
    valorDurumu?: string | null;
}

export interface ParaBirimiOzetModel {
    paraBirimi: string;
    toplamNakit: number;
    toplamBankaMuhasebeBakiyesi: number;
    toplamBekleyenNetPos: number;
    tahminiToplamBankaPozisyonu: number;
}

export interface NakitBankaPozisyonuOzetModel {
    raporTarihi: string;
    toplamNakit: number;
    toplamBankaMuhasebeBakiyesi: number;
    valoruGecmisBekleyenNet: number;
    bugunGelecekNet: number;
    yarinGelecekNet: number;
    takip2_7GunGelecekNet: number;
    sonraki7GundenSonraNet: number;
    toplamBekleyenNetPos: number;
    tahminiToplamBankaPozisyonu: number;
    mutabakatBekleyenToplam: number;
    mutabakatBekleyenAdet: number;
    hataliToplam: number;
    hataliAdet: number;
    /** true ise secilen rapor tarihi bugunden oncesidir. */
    gecmisTarihRaporuMu: boolean;
    /** false ise POS/valor pozisyonu HIC hesaplanmamistir (gecmis tarih) - tum POS alanlari 0'dir. */
    posPozisyonuHesaplandiMi: boolean;
    posPozisyonuHesaplanmamaNedeni?: string | null;
    uyariSayisi: number;
    uyariliTutarlar: UyariliTutarOzetiModel[];
    paraBirimiOzetleri: ParaBirimiOzetModel[];
}

/** Normal finansal toplamin DISINDA tutulan tutarlarin neden + para birimi bazinda ozeti. */
export interface UyariliTutarOzetiModel {
    uyariTipi: string;
    paraBirimi: string;
    adet: number;
    toplamNetTutar: number;
    aciklama: string;
}

export interface NakitHesapPozisyonuModel {
    kasaBankaHesapId: number;
    tesisId: number;
    ad: string;
    kod: string;
    paraBirimi: string;
    muhasebeHesapPlaniId?: number | null;
    muhasebeHesapKodu?: string | null;
    muhasebeHesapAdi?: string | null;
    muhasebeBakiyesi: number;
    sonHareketTarihi?: string | null;
}

export interface BankaHesapPozisyonuModel {
    kasaBankaHesapId: number;
    tesisId: number;
    bankaAdi: string;
    hesapAdi: string;
    iban?: string | null;
    paraBirimi: string;
    muhasebeHesapPlaniId?: number | null;
    muhasebeHesapKodu?: string | null;
    stysMuhasebeBakiyesi: number;
    valoruGecmisBekleyenNet: number;
    bugunGelecekNet: number;
    yarinGelecekNet: number;
    takip2_7GunGelecekNet: number;
    sonraki7GundenSonraNet: number;
    toplamBekleyenNet: number;
    /** null ise hesabin gecerli bir muhasebe baglantisi yoktur - sahte "bakiye" uretilmez. */
    tahminiBakiye: number | null;
    muhasebeBakiyesiGecerliMi: boolean;
    mutabakatBekleyenNet: number;
    mutabakatBekleyenAdet: number;
    hataliNet: number;
    hataliAdet: number;
    uyariliTutarlar: UyariliTutarOzetiModel[];
    sonMuhasebeHareketTarihi?: string | null;
}

export interface VeriKalitesiUyariModel {
    uyariTipi: string;
    aciklama: string;
    kasaBankaHesapId?: number | null;
    posTahsilatValorId?: number | null;
    tutar?: number | null;
    paraBirimi?: string | null;
    adet: number;
}

/** GetPozisyonAsync'in tek, birlesik sonucu - ozet + hesap listeleri + uyarilar TEK cagriden gelir. */
export interface NakitBankaPozisyonuModel {
    raporTarihi: string;
    gecmisTarihRaporuMu: boolean;
    posPozisyonuHesaplandiMi: boolean;
    posPozisyonuHesaplanmamaNedeni?: string | null;
    ozet: NakitBankaPozisyonuOzetModel;
    kasaHesaplari: NakitHesapPozisyonuModel[];
    bankaHesaplari: BankaHesapPozisyonuModel[];
    uyarilar: VeriKalitesiUyariModel[];
    uygulananFiltre: NakitBankaPozisyonuFilterModel;
}

export interface ValorDetayModel {
    id: number;
    tahsilatOdemeBelgesiId: number;
    tahsilatBelgeNo?: string | null;
    krediKartiHesapAdi?: string | null;
    odemeTarihi: string;
    beklenenValorTarihi: string;
    brutTutar: number;
    komisyonTutari: number;
    netTutar: number;
    durum: string;
    muhasebeFisId?: number | null;
    hataMesaji?: string | null;
}

/** Sunucu tarafli sayfalama sonucu - TOD.Platform PagedResult<T> ile birebir eslesir. */
export interface PagedResultModel<T> {
    items: T[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

/** Yalnizca gun bazinda OZET - detay satirlari icermez (bkz. getValorGunDetaylari, ayri sayfali
 * bir sorgudur). Kullanici bir gunu actiginda yalnizca o gunun sayfali detaylari ayrica yuklenir. */
export interface GunlukValorOzetiModel {
    valorTarihi: string;
    islemSayisi: number;
    brutTutar: number;
    komisyonTutari: number;
    netTutar: number;
}

export interface BankaValorTakvimiModel {
    kasaBankaHesapId: number;
    raporTarihi: string;
    gunler: GunlukValorOzetiModel[];
}

export const HESAP_TURU_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Kasa', value: 'Kasa' },
    { label: 'Banka', value: 'Banka' }
];

export const VALOR_DURUMU_SECENEKLERI: Array<{ label: string; value: string | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Valör Bekliyor', value: 'ValorBekliyor' },
    { label: 'Mutabakat Bekliyor', value: 'MutabakatBekliyor' },
    { label: 'Hata', value: 'Hata' }
];

export const VERI_KALITESI_UYARI_LABELLARI: Record<string, string> = {
    IbanVarMuhasebeHesabiYok: 'IBAN tanımlı, muhasebe hesabı yok',
    MuhasebeHesabiVarIbanYok: 'Muhasebe hesabı var, IBAN yok',
    PosValorHedefBankaBelirlenemiyor: 'POS valörün hedef bankası belirlenemiyor',
    NetVeyaKomisyonBilgisiEksik: 'Net/komisyon bilgisi eksik',
    ValorTarihiBos: 'Valör tarihi boş',
    AktarimDurumuFisIliskisiTutarsiz: 'Aktarım durumu / fiş ilişkisi tutarsız',
    AyniMuhasebeHesabinaBirdenFazlaAktifBankaHesabiBagli: 'Aynı tesiste aynı muhasebe hesabına birden fazla aktif banka hesabı bağlı',
    AyniBankaHesabiBirdenFazlaMuhasebeHesabinaBagli: 'Aynı banka hesabı birden fazla aktif muhasebe hesabına bağlı',
    SoftDeleteEdilmisBaglantiliMuhasebeHesabi: 'Bağlı muhasebe hesabı silinmiş',
    PasifBaglantiliMuhasebeHesabi: 'Bağlı muhasebe hesabı pasif',
    BankaHesabininMuhasebeBaglantisiGecersiz: 'Banka hesabının geçerli muhasebe bağlantısı yok',
    BankaHesabiBulunamadiVeyaPasif: 'Bağlı banka hesabı bulunamadı veya pasif',
    ParaBirimiUyusmuyor: 'Para birimi banka hesabıyla uyuşmuyor',
    TaninmayanValorDurumu: 'Tanınmayan valör durumu (güvenli davranışla toplam dışı bırakıldı)',
    MutabakatBekleyen: 'Mutabakat bekliyor',
    HataliValor: 'Hatalı valör kaydı',
    AktarimSurecindeValor: 'Aktarım/ters kayıt süreci devam ediyor',
    GecmisTarihPosPozisyonuHesaplanmadi: 'Geçmiş tarihte POS pozisyonu hesaplanmadı'
};

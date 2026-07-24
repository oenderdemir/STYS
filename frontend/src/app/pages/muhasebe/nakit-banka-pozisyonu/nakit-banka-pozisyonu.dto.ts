export interface NakitBankaPozisyonuFilterModel {
    tesisId?: number | null;
    raporTarihi?: string | null;
    maliYil?: number | null;
    donem?: number | null;
    hesapTuru?: string | null;
    bankaHesapId?: number | null;
    paraBirimi?: string | null;
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
    uyariSayisi: number;
    paraBirimiOzetleri: ParaBirimiOzetModel[];
}

export interface NakitHesapPozisyonuModel {
    kasaBankaHesapId: number;
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
    tahminiBakiye: number;
    mutabakatBekleyenNet: number;
    mutabakatBekleyenAdet: number;
    hataliNet: number;
    hataliAdet: number;
    sonMuhasebeHareketTarihi?: string | null;
}

export interface VeriKalitesiUyariModel {
    uyariTipi: string;
    aciklama: string;
    kasaBankaHesapId?: number | null;
    posTahsilatValorId?: number | null;
    tutar?: number | null;
}

export interface NakitBankaPozisyonuHesaplarModel {
    raporTarihi: string;
    kasaHesaplari: NakitHesapPozisyonuModel[];
    bankaHesaplari: BankaHesapPozisyonuModel[];
    uyarilar: VeriKalitesiUyariModel[];
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

export interface GunlukValorOzetiModel {
    valorTarihi: string;
    islemSayisi: number;
    brutTutar: number;
    komisyonTutari: number;
    netTutar: number;
    detaylar: ValorDetayModel[];
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
    AyniBankaHesabinaBirdenFazlaAktifMuhasebeHesabi: 'Aynı bankaya birden fazla aktif muhasebe hesabı bağlı',
    SoftDeleteEdilmisBaglantiliMuhasebeHesabi: 'Bağlı muhasebe hesabı silinmiş'
};

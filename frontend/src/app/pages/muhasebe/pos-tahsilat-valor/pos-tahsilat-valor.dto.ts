export interface PosTahsilatValorModel {
    id?: number;
    tesisId: number;
    tahsilatOdemeBelgesiId: number;
    tahsilatBelgeNo?: string | null;
    krediKartiHesapId: number;
    krediKartiHesapAdi?: string | null;
    bagliBankaHesapId?: number | null;
    bagliBankaHesapAdi?: string | null;
    komisyonGiderHesapPlaniId?: number | null;
    odemeTarihi: string;
    valorGunSayisi: number;
    valorGunTuru: string;
    beklenenValorTarihi: string;
    otomatikAktarimMi: boolean;
    komisyonOraniSnapshot?: number | null;
    brutTutar: number;
    komisyonTutari: number;
    netTutar: number;
    paraBirimi: string;
    durum: string;
    aktarimTarihi?: string | null;
    muhasebeFisId?: number | null;
    tersKayitMuhasebeFisId?: number | null;
    hataMesaji?: string | null;
    aciklama?: string | null;
    denemeSayisi: number;
    valoreKalanGun: number;
    valorGectiMi: boolean;
    bugunValorGunuMu: boolean;
    aktarilabilirMi: boolean;
}

export interface PosTahsilatValorOzetModel {
    valorBekleyenToplam: number;
    valorBekleyenAdet: number;
    bugunValoruGelenToplam: number;
    bugunValoruGelenAdet: number;
    valoruGecmisToplam: number;
    valoruGecmisAdet: number;
    aktarilanToplam: number;
    aktarilanAdet: number;
    hataliAdet: number;
}

export interface ManuelAktarimGuncellemeRequest {
    komisyonTutari?: number | null;
    netTutar?: number | null;
    komisyonGiderHesapPlaniIdOverride?: number | null;
    aciklama?: string | null;
}

export interface PosTahsilatValorAktarimSonucModel {
    id: number;
    basarili: boolean;
    hataMesaji?: string | null;
    muhasebeFisId?: number | null;
}

export interface PosTahsilatValorToplamAktarimSonucModel {
    basarili: PosTahsilatValorAktarimSonucModel[];
    hatali: PosTahsilatValorAktarimSonucModel[];
}

export interface PosTahsilatValorTopluOnayBilgisiRequest {
    valorIdler?: number[] | null;
    tesisId?: number | null;
    sadeceValoruGelenler?: boolean;
}

export interface PosTahsilatValorTopluOnayBilgisiModel {
    adet: number;
    toplamBrut: number;
    toplamKomisyon: number;
    toplamNet: number;
}

export const POS_VALOR_DURUM_LABELLARI: Record<string, string> = {
    ValorBekliyor: 'Valör Bekliyor',
    MutabakatBekliyor: 'Mutabakat Bekliyor',
    Aktariliyor: 'Aktarılıyor',
    Aktarildi: 'Aktarıldı',
    Hata: 'Hata',
    Iptal: 'İptal',
    AktarimFisiIptalEdildi: 'Aktarım Fişi İptal Edildi',
    TersKayitOlusturuluyor: 'Düzeltme İşleniyor'
};

export const POS_VALOR_DURUM_SEVERITY: Record<string, 'info' | 'warn' | 'secondary' | 'success' | 'danger' | 'contrast'> = {
    ValorBekliyor: 'info',
    MutabakatBekliyor: 'warn',
    Aktariliyor: 'secondary',
    Aktarildi: 'success',
    Hata: 'danger',
    Iptal: 'secondary',
    AktarimFisiIptalEdildi: 'contrast',
    TersKayitOlusturuluyor: 'secondary'
};

export interface StokHareketModel {
    id?: number;
    depoId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    hareketTarihi: string;
    hareketTipi: string;
    miktar: number;
    birimFiyat: number;
    tutar: number;
    belgeNo?: string | null;
    belgeTarihi?: string | null;
    aciklama?: string | null;
    cariKartId?: number | null;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    transferGrupId?: string | null;
    transferYonu?: string | null;
    sayimFarkiYonu?: string | null;
    karsiDepoId?: number | null;
    hedefDepoId?: number | null;
    durum: string;
    kdvUygulamaTipi: number;
    kdvIstisnaTanimId?: number | null;
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    kdvOrani: number;
    kdvTutari: number;
}

/** Create/Update payload — snapshot fields (KdvIstisnaKodu, KdvIstisnaAciklamasi, KdvTutari) server'da hesaplanır. */
export interface CreateStokHareketRequest {
    depoId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    hareketTarihi: string;
    hareketTipi: string;
    miktar: number;
    birimFiyat: number;
    belgeNo?: string | null;
    belgeTarihi?: string | null;
    aciklama?: string | null;
    cariKartId?: number | null;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    sayimFarkiYonu?: string | null;
    durum: string;
    kdvUygulamaTipi: number;
    kdvIstisnaTanimId?: number | null;
    kdvOrani: number;
}

export interface UpdateStokHareketRequest extends CreateStokHareketRequest {}

export interface StokTransferRequest {
    kaynakDepoId: number;
    hedefDepoId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    hareketTarihi: string;
    miktar: number;
    birimFiyat: number;
    belgeNo?: string | null;
    belgeTarihi?: string | null;
    aciklama?: string | null;
}

export interface StokBakiyeModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
}

export interface StokKartOzetModel {
    tasinirKartId: number;
    stokKodu: string;
    ad: string;
    birim: string;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
}

export interface StokDetayModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    malzemeKayitTipi: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
    aciklama: string;
    satirlar: StokDetaySatirModel[];
}

export interface StokDetaySatirModel {
    hareketTarihi?: string | null;
    lotNo?: string | null;
    sonKullanmaTarihi?: string | null;
    miktar: number;
    birim: string;
    birimFiyat: number;
    toplamTutar: number;
    hareketSayisi: number;
}

export interface StokLotBakiyeModel {
    stokLotId: number;
    lotNo: string;
    sonKullanmaTarihi?: string | null;
    girisMiktari: number;
    cikisMiktari: number;
    bakiyeMiktari: number;
}

export const STOK_HAREKET_TIPLERI: Array<{ label: string; value: string }> = [
    { label: 'Giris', value: 'Giris' },
    { label: 'Cikis', value: 'Cikis' },
    { label: 'Transfer', value: 'Transfer' },
    { label: 'Iade', value: 'Iade' },
    { label: 'Sarf', value: 'Sarf' },
    { label: 'Sayim Farki', value: 'SayimFarki' },
    { label: 'Zimmet', value: 'Zimmet' }
];

export const STOK_HAREKET_DURUMLARI: Array<{ label: string; value: string }> = [
    { label: 'Aktif', value: 'Aktif' },
    { label: 'Iptal', value: 'Iptal' }
];

export const STOK_SAYIM_FARKI_YONLERI: Array<{ label: string; value: string }> = [
    { label: 'Stok Fazlası', value: 'Fazla' },
    { label: 'Stok Eksiği', value: 'Eksik' }
];

export interface StokHareketModel {
    id?: number;
    depoId: number;
    tasinirKartId: number;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    lotNo?: string | null;
    seriNo?: string | null;
    sonKullanmaTarihi?: string | null;
    hareketTarihi: string;
    hareketTipi: string;
    miktar: number;
    birimFiyat: number;
    tutar: number;
    maliyetBirimFiyat?: number | null;
    maliyetTutari?: number | null;
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
    stokSeriId?: number | null;
    lotNo?: string | null;
    seriNo?: string | null;
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
    stokSeriId?: number | null;
    seriNo?: string | null;
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

export interface StokDegerlemeModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    bakiyeMiktari: number;
    ortalamaMaliyet: number;
    toplamStokDegeri: number;
    maliyetEksikMi: boolean;
}

export interface StokMaliyetPolitikasiModel {
    id: number;
    tesisId: number;
    maliYil: number;
    maliyetYontemi: string;
}

export interface CurrentStokMaliyetPolitikasiModel {
    tesisId: number;
    maliYil: number;
    maliyetYontemi?: string | null;
    politikaSecildiMi: boolean;
}

export interface UpsertStokMaliyetPolitikasiRequest {
    tesisId: number;
    maliYil: number;
    maliyetYontemi: string;
}

export interface FifoBaslangicStoguSatirModel {
    depoId: number;
    depoKod: string;
    depoAd: string;
    tasinirKartId: number;
    stokKodu: string;
    tasinirKartAd: string;
    birim: string;
    mevcutStokMiktari: number;
    fifoKatmanMiktari: number;
    katmansizMiktar: number;
    onerilenBirimMaliyet?: number | null;
    maliyetGuvenilirMi: boolean;
    birimMaliyet?: number | null;
}

export interface CreateFifoBaslangicStoguRequest {
    tesisId: number;
    maliYil: number;
    satirlar: CreateFifoBaslangicStoguSatirRequest[];
}

export interface CreateFifoBaslangicStoguSatirRequest {
    depoId: number;
    tasinirKartId: number;
    birimMaliyet: number;
}

export const STOK_MALIYET_YONTEMI_SECENEKLERI: Array<{ label: string; value: string; disabled?: boolean }> = [
    { label: 'Ağırlıklı Ortalama', value: 'AgirlikliOrtalama' },
    { label: 'FIFO', value: 'FIFO' },
    { label: 'LIFO', value: 'LIFO' }
];

export interface StokDetaySatirModel {
    hareketTarihi?: string | null;
    stokLotId?: number | null;
    stokSeriId?: number | null;
    lotNo?: string | null;
    seriNo?: string | null;
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

export interface StokSeriBakiyeModel {
    stokSeriId: number;
    seriNo: string;
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

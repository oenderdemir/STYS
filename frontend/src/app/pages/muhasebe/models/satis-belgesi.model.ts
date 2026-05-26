// ──────────────────────────────────────────────
//  Satış Belgesi / Fatura Taslak — Frontend Model
// ──────────────────────────────────────────────

// ── Enums ──

export enum SatisBelgesiDurumu {
    Taslak = 1,
    MuhasebeOnayinda = 2,
    MuhasebeOnaylandi = 3,
    Reddedildi = 4,
    FaturaKesildi = 5,
    MusteriyeGonderildi = 6,
    IptalEdildi = 7
}

export enum SatisBelgesiTipi {
    FaturaTaslagi = 1,
    SatisFaturasi = 2,
    IadeFaturasi = 3,
    Proforma = 4,
    AlisFaturasi = 5,
    SatisIadeFaturasi = 6,
    AlisIadeFaturasi = 7
}

export enum SatisKaynakModulu {
    Manuel = 1,
    Otel = 2,
    Restoran = 3,
    Kamp = 4,
    EkHizmet = 5,
    Diger = 99
}

export enum SatisBelgesiSatirTipi {
    Konaklama = 1,
    YiyecekIcecek = 2,
    KampHizmeti = 3,
    EkHizmet = 4,
    Urun = 5,
    Indirim = 6,
    Iade = 7,
    Diger = 99
}

export enum KdvUygulamaTipi {
    Kdvli = 1,
    TamIstisna = 2,
    KismiIstisna = 3,
    KdvKapsamDisi = 4,
    Tevkifatli = 5
}

// ── Label maps ──

export const SATIS_BELGESI_DURUMU_LABELS: Record<SatisBelgesiDurumu, string> = {
    [SatisBelgesiDurumu.Taslak]: 'Taslak',
    [SatisBelgesiDurumu.MuhasebeOnayinda]: 'Muhasebe Onayında',
    [SatisBelgesiDurumu.MuhasebeOnaylandi]: 'Muhasebe Onaylandı',
    [SatisBelgesiDurumu.Reddedildi]: 'Reddedildi',
    [SatisBelgesiDurumu.FaturaKesildi]: 'Fatura Kesildi',
    [SatisBelgesiDurumu.MusteriyeGonderildi]: 'Müşteriye Gönderildi',
    [SatisBelgesiDurumu.IptalEdildi]: 'İptal Edildi'
};

export const SATIS_BELGESI_DURUMU_SEVERITIES: Record<SatisBelgesiDurumu, string> = {
    [SatisBelgesiDurumu.Taslak]: 'info',
    [SatisBelgesiDurumu.MuhasebeOnayinda]: 'warn',
    [SatisBelgesiDurumu.MuhasebeOnaylandi]: 'success',
    [SatisBelgesiDurumu.Reddedildi]: 'danger',
    [SatisBelgesiDurumu.FaturaKesildi]: 'success',
    [SatisBelgesiDurumu.MusteriyeGonderildi]: 'success',
    [SatisBelgesiDurumu.IptalEdildi]: 'secondary'
};

export const SATIS_BELGESI_TIPI_LABELS: Record<SatisBelgesiTipi, string> = {
    [SatisBelgesiTipi.FaturaTaslagi]: 'Fatura Taslağı',
    [SatisBelgesiTipi.SatisFaturasi]: 'Satış Faturası',
    [SatisBelgesiTipi.IadeFaturasi]: 'İade Faturası (Legacy)',
    [SatisBelgesiTipi.Proforma]: 'Proforma',
    [SatisBelgesiTipi.AlisFaturasi]: 'Alış Faturası',
    [SatisBelgesiTipi.SatisIadeFaturasi]: 'Satış İade Faturası',
    [SatisBelgesiTipi.AlisIadeFaturasi]: 'Alış İade Faturası'
};

export const SATIS_BELGE_TIPLERI: SatisBelgesiTipi[] = [
    SatisBelgesiTipi.FaturaTaslagi,
    SatisBelgesiTipi.SatisFaturasi,
    SatisBelgesiTipi.SatisIadeFaturasi,
    SatisBelgesiTipi.Proforma,
    SatisBelgesiTipi.IadeFaturasi
];

export const ALIS_BELGE_TIPLERI: SatisBelgesiTipi[] = [
    SatisBelgesiTipi.AlisFaturasi,
    SatisBelgesiTipi.AlisIadeFaturasi
];

export const SATIS_KAYNAK_MODULU_LABELS: Record<SatisKaynakModulu, string> = {
    [SatisKaynakModulu.Manuel]: 'Manuel',
    [SatisKaynakModulu.Otel]: 'Otel',
    [SatisKaynakModulu.Restoran]: 'Restoran',
    [SatisKaynakModulu.Kamp]: 'Kamp',
    [SatisKaynakModulu.EkHizmet]: 'Ek Hizmet',
    [SatisKaynakModulu.Diger]: 'Diğer'
};

export const SATIS_BELGESI_SATIR_TIPI_LABELS: Record<SatisBelgesiSatirTipi, string> = {
    [SatisBelgesiSatirTipi.Konaklama]: 'Konaklama',
    [SatisBelgesiSatirTipi.YiyecekIcecek]: 'Yiyecek & İçecek',
    [SatisBelgesiSatirTipi.KampHizmeti]: 'Kamp Hizmeti',
    [SatisBelgesiSatirTipi.EkHizmet]: 'Ek Hizmet',
    [SatisBelgesiSatirTipi.Urun]: 'Ürün',
    [SatisBelgesiSatirTipi.Indirim]: 'İndirim',
    [SatisBelgesiSatirTipi.Iade]: 'İade',
    [SatisBelgesiSatirTipi.Diger]: 'Diğer'
};

export const KDV_UYGULAMA_TIPI_LABELS: Record<KdvUygulamaTipi, string> = {
    [KdvUygulamaTipi.Kdvli]: 'KDV\'li',
    [KdvUygulamaTipi.TamIstisna]: 'Tam İstisna',
    [KdvUygulamaTipi.KismiIstisna]: 'Kısmi İstisna',
    [KdvUygulamaTipi.KdvKapsamDisi]: 'KDV Kapsam Dışı',
    [KdvUygulamaTipi.Tevkifatli]: 'Tevkifatlı'
};

// ── Durum seçenekleri (filter dropdown için) ──

export const SATIS_BELGESI_DURUM_SECENEKLERI = Object.entries(SATIS_BELGESI_DURUMU_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);

// ── DTO interfaces ──

export interface SatisBelgesiSatiriDto {
    id: number;
    satisBelgesiId: number;
    siraNo: number;
    satirTipi: SatisBelgesiSatirTipi;
    aciklama: string;
    tasinirKartId?: number | null;
    depoId?: number | null;
    birim: string;
    miktar: number;
    birimFiyat: number;
    indirimTutari: number;
    matrah: number;
    kdvUygulamaTipi: KdvUygulamaTipi;
    kdvIstisnaTanimId?: number | null;
    kdvIstisnaKodu?: string | null;
    kdvIstisnaAciklamasi?: string | null;
    kdvOrani: number;
    kdvTutari: number;
    tevkifatPay?: number | null;
    tevkifatPayda?: number | null;
    tevkifatTutari: number;
    netKdv: number;
    satirToplami: number;
    kaynakSatirId?: string | null;
}

export interface SatisBelgesiDto {
    id: number;
    belgeNo: string;
    belgeTipi: SatisBelgesiTipi;
    durum: SatisBelgesiDurumu;
    kaynakModul: SatisKaynakModulu;
    kaynakTipi?: string | null;
    kaynakId?: string | null;
    tesisId?: number | null;
    cariKartId?: number | null;
    cariKartKodu?: string | null;
    cariKartUnvanAdSoyad?: string | null;
    cariKartTipi?: string | null;
    cariKartVergiNoTckn?: string | null;
    belgeTarihi: string;
    vadeTarihi?: string | null;
    musteriUnvan?: string | null;
    musteriAdSoyad?: string | null;
    musteriVergiNo?: string | null;
    musteriTcKimlikNo?: string | null;
    musteriVergiDairesi?: string | null;
    musteriAdres?: string | null;
    musteriEposta?: string | null;
    musteriTelefon?: string | null;
    kurumsalMi: boolean;
    toplamMatrah: number;
    toplamKdv: number;
    toplamTevkifatTutari: number;
    toplamNetKdv: number;
    genelToplam: number;
    aciklama?: string | null;
    redNedeni?: string | null;
    resmiFaturaNo?: string | null;
    eBelgeUuid?: string | null;
    muhasebeOnayinaGonderilmeTarihi?: string | null;
    muhasebeOnayTarihi?: string | null;
    faturaKesimTarihi?: string | null;
    musteriyeGonderimTarihi?: string | null;
    muhasebeFisId?: number | null;
    muhasebeFisOlusturmaTarihi?: string | null;
    satirlar: SatisBelgesiSatiriDto[];
}

export interface CreateSatisBelgesiSatiriRequest {
    siraNo: number;
    satirTipi: SatisBelgesiSatirTipi;
    aciklama: string;
    tasinirKartId?: number | null;
    depoId?: number | null;
    birim: string;
    miktar: number;
    birimFiyat: number;
    indirimTutari: number;
    kdvUygulamaTipi: KdvUygulamaTipi;
    kdvIstisnaTanimId?: number | null;
    kdvOrani: number;
    tevkifatPay?: number | null;
    tevkifatPayda?: number | null;
    kaynakSatirId?: string | null;
}

export interface CreateSatisBelgesiRequest {
    belgeTipi: SatisBelgesiTipi;
    kaynakModul: SatisKaynakModulu;
    kaynakTipi?: string | null;
    kaynakId?: string | null;
    tesisId?: number | null;
    cariKartId?: number | null;
    belgeTarihi: string;
    vadeTarihi?: string | null;
    musteriUnvan?: string | null;
    musteriAdSoyad?: string | null;
    musteriVergiNo?: string | null;
    musteriTcKimlikNo?: string | null;
    musteriVergiDairesi?: string | null;
    musteriAdres?: string | null;
    musteriEposta?: string | null;
    musteriTelefon?: string | null;
    kurumsalMi: boolean;
    aciklama?: string | null;
    belgeNo?: string | null;
    satirlar: CreateSatisBelgesiSatiriRequest[];
}

export interface UpdateSatisBelgesiRequest {
    belgeNo?: string | null;
    belgeTipi?: SatisBelgesiTipi | null;
    tesisId?: number | null;
    cariKartId?: number | null;
    belgeTarihi?: string | null;
    vadeTarihi?: string | null;
    musteriUnvan?: string | null;
    musteriAdSoyad?: string | null;
    musteriVergiNo?: string | null;
    musteriTcKimlikNo?: string | null;
    musteriVergiDairesi?: string | null;
    musteriAdres?: string | null;
    musteriEposta?: string | null;
    musteriTelefon?: string | null;
    kurumsalMi?: boolean | null;
    aciklama?: string | null;
    satirlar?: CreateSatisBelgesiSatiriRequest[] | null;
}

export interface SatisBelgesiFilterDto {
    tesisId?: number | null;
    belgeTipleri?: SatisBelgesiTipi[] | null;
    durum?: SatisBelgesiDurumu | null;
    kaynakModul?: SatisKaynakModulu | null;
    kaynakTipi?: string | null;
    kaynakId?: string | null;
    belgeNo?: string | null;
    musteri?: string | null;
    baslangicTarihi?: string | null;
    bitisTarihi?: string | null;
}

export interface SatisBelgesiRedRequest {
    redNedeni: string;
}

// ── Default helpers ──

export function createDefaultSatisBelgesiFilter(): SatisBelgesiFilterDto {
    return {
        tesisId: null,
        belgeTipleri: null,
        durum: null,
        kaynakModul: null,
        kaynakTipi: null,
        kaynakId: null,
        belgeNo: null,
        musteri: null,
        baslangicTarihi: null,
        bitisTarihi: null
    };
}

export function createEmptySatisBelgesiSatiri(): CreateSatisBelgesiSatiriRequest {
    return {
        siraNo: 1,
        satirTipi: SatisBelgesiSatirTipi.Diger,
        aciklama: '',
        tasinirKartId: null,
        depoId: null,
        birim: 'Adet',
        miktar: 1,
        birimFiyat: 0,
        indirimTutari: 0,
        kdvUygulamaTipi: KdvUygulamaTipi.Kdvli,
        kdvIstisnaTanimId: null,
        kdvOrani: 20,
        tevkifatPay: null,
        tevkifatPayda: null,
        kaynakSatirId: null
    };
}

export function createEmptyCreateSatisBelgesiRequest(): CreateSatisBelgesiRequest {
    return {
        belgeTipi: SatisBelgesiTipi.FaturaTaslagi,
        kaynakModul: SatisKaynakModulu.Manuel,
        kaynakTipi: null,
        kaynakId: null,
        tesisId: null,
        cariKartId: null,
        belgeTarihi: new Date().toISOString().split('T')[0],
        vadeTarihi: null,
        musteriUnvan: null,
        musteriAdSoyad: null,
        musteriVergiNo: null,
        musteriTcKimlikNo: null,
        musteriVergiDairesi: null,
        musteriAdres: null,
        musteriEposta: null,
        musteriTelefon: null,
        kurumsalMi: false,
        aciklama: null,
        belgeNo: null,
        satirlar: [createEmptySatisBelgesiSatiri()]
    };
}

export function isAlisBelgeTipi(belgeTipi: SatisBelgesiTipi): boolean {
    return ALIS_BELGE_TIPLERI.includes(belgeTipi);
}

export function isSatisBelgeTipi(belgeTipi: SatisBelgesiTipi): boolean {
    return SATIS_BELGE_TIPLERI.includes(belgeTipi) || belgeTipi === SatisBelgesiTipi.IadeFaturasi;
}

// ── Helper: müşteri display adı ──

export function getMusteriDisplayName(belge: SatisBelgesiDto): string {
    if (belge.kurumsalMi) {
        return belge.musteriUnvan ?? belge.musteriAdSoyad ?? '-';
    }
    return belge.musteriAdSoyad ?? belge.musteriUnvan ?? '-';
}

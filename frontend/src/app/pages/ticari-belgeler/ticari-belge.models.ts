// ──────────────────────────────────────────────
//  Ticari Belge — Operasyon Frontend Modeli
//  Backend TicariBelge DTO'larıyla (backend/TicariBelgeler/Dtos) uyumludur.
//  BİLİNÇLİ OLARAK İÇERMEZ: legacy Durum, MuhasebeFisId, muhasebe fişi satırları,
//  hesap planı kodları, borç/alacak bilgileri, e-belge/entegratör teknik alanları.
// ──────────────────────────────────────────────
import { toLocalDateString } from '../../core/utils/date-time.util';

export type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

// ── Enumlar (backend STYS.Muhasebe.SatisBelgeleri.Enums ile birebir) ──

export enum TicariBelgeDurumu {
    Taslak = 1,
    Hazir = 2,
    IptalEdildi = 3
}

export enum TicariBelgeMuhasebeDurumu {
    Bekliyor = 1,
    Onayda = 2,
    Onaylandi = 3,
    Reddedildi = 4,
    IptalEdildi = 5
}

export enum TicariBelgeFaturalamaDurumu {
    Uygulanamaz = 1,
    Baslatilmadi = 2,
    KesimBekliyor = 3,
    Kesildi = 4,
    MusteriyeGonderildi = 5,
    IptalEdildi = 6
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

// ── Label / severity haritaları — legacy Durum yorumlama mantığı YOKTUR ──

export const TICARI_BELGE_DURUMU_LABELS: Record<TicariBelgeDurumu, string> = {
    [TicariBelgeDurumu.Taslak]: 'Taslak',
    [TicariBelgeDurumu.Hazir]: 'Hazır',
    [TicariBelgeDurumu.IptalEdildi]: 'İptal Edildi'
};

export const TICARI_BELGE_DURUMU_SEVERITIES: Record<TicariBelgeDurumu, TagSeverity> = {
    [TicariBelgeDurumu.Taslak]: 'info',
    [TicariBelgeDurumu.Hazir]: 'success',
    [TicariBelgeDurumu.IptalEdildi]: 'secondary'
};

export const MUHASEBE_DURUMU_LABELS: Record<TicariBelgeMuhasebeDurumu, string> = {
    [TicariBelgeMuhasebeDurumu.Bekliyor]: 'Bekliyor',
    [TicariBelgeMuhasebeDurumu.Onayda]: 'Onayda',
    [TicariBelgeMuhasebeDurumu.Onaylandi]: 'Onaylandı',
    [TicariBelgeMuhasebeDurumu.Reddedildi]: 'Reddedildi',
    [TicariBelgeMuhasebeDurumu.IptalEdildi]: 'İptal Edildi'
};

export const MUHASEBE_DURUMU_SEVERITIES: Record<TicariBelgeMuhasebeDurumu, TagSeverity> = {
    [TicariBelgeMuhasebeDurumu.Bekliyor]: 'secondary',
    [TicariBelgeMuhasebeDurumu.Onayda]: 'warn',
    [TicariBelgeMuhasebeDurumu.Onaylandi]: 'success',
    [TicariBelgeMuhasebeDurumu.Reddedildi]: 'danger',
    [TicariBelgeMuhasebeDurumu.IptalEdildi]: 'secondary'
};

export const FATURALAMA_DURUMU_LABELS: Record<TicariBelgeFaturalamaDurumu, string> = {
    [TicariBelgeFaturalamaDurumu.Uygulanamaz]: 'Uygulanamaz',
    [TicariBelgeFaturalamaDurumu.Baslatilmadi]: 'Başlatılmadı',
    [TicariBelgeFaturalamaDurumu.KesimBekliyor]: 'Kesim Bekliyor',
    [TicariBelgeFaturalamaDurumu.Kesildi]: 'Kesildi',
    [TicariBelgeFaturalamaDurumu.MusteriyeGonderildi]: 'Müşteriye Gönderildi',
    [TicariBelgeFaturalamaDurumu.IptalEdildi]: 'İptal Edildi'
};

export const FATURALAMA_DURUMU_SEVERITIES: Record<TicariBelgeFaturalamaDurumu, TagSeverity> = {
    [TicariBelgeFaturalamaDurumu.Uygulanamaz]: 'secondary',
    [TicariBelgeFaturalamaDurumu.Baslatilmadi]: 'info',
    [TicariBelgeFaturalamaDurumu.KesimBekliyor]: 'warn',
    [TicariBelgeFaturalamaDurumu.Kesildi]: 'success',
    [TicariBelgeFaturalamaDurumu.MusteriyeGonderildi]: 'success',
    [TicariBelgeFaturalamaDurumu.IptalEdildi]: 'secondary'
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

export const TICARI_BELGE_DURUM_SECENEKLERI = Object.entries(TICARI_BELGE_DURUMU_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);
export const MUHASEBE_DURUMU_SECENEKLERI = Object.entries(MUHASEBE_DURUMU_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);
export const FATURALAMA_DURUMU_SECENEKLERI = Object.entries(FATURALAMA_DURUMU_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);
export const SATIS_BELGESI_TIPI_SECENEKLERI = Object.entries(SATIS_BELGESI_TIPI_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);
export const SATIS_KAYNAK_MODULU_SECENEKLERI = Object.entries(SATIS_KAYNAK_MODULU_LABELS).map(
    ([key, label]) => ({ value: Number(key), label })
);

// ── DTO arayüzleri ──

export interface TicariBelgeSatirDto {
    id: number;
    siraNo: number;
    satirTipi: SatisBelgesiSatirTipi;
    aciklama: string;
    birim: string;
    miktar: number;
    birimFiyat: number;
    indirimOrani: number;
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
    otvOrani: number;
    otvTutari: number;
    oivOrani: number;
    oivTutari: number;
    konaklamaVergisiOrani: number;
    konaklamaVergisiTutari: number;
    netKdv: number;
    satirToplami: number;
    kaynakSatirId?: string | null;
}

export interface TicariBelgeDto {
    id: number;
    belgeNo: string;
    belgeTipi: SatisBelgesiTipi;
    ticariDurum: TicariBelgeDurumu;
    muhasebeDurumu: TicariBelgeMuhasebeDurumu;
    faturalamaDurumu: TicariBelgeFaturalamaDurumu;
    operasyonelDurumAciklamasi: string;
    kaynakModul: SatisKaynakModulu;
    kaynakTipi?: string | null;
    kaynakId?: string | null;
    tesisId?: number | null;
    cariKartId?: number | null;
    belgeTarihi: string;
    vadeTarihi?: string | null;
    kurumsalMi: boolean;
    musteriUnvan?: string | null;
    musteriAdSoyad?: string | null;
    musteriVergiNo?: string | null;
    musteriTcKimlikNo?: string | null;
    musteriVergiDairesi?: string | null;
    musteriAdres?: string | null;
    musteriEposta?: string | null;
    musteriTelefon?: string | null;
    toplamMatrah: number;
    toplamKdv: number;
    toplamTevkifatTutari: number;
    toplamNetKdv: number;
    genelToplam: number;
    aciklama?: string | null;
    redNedeni?: string | null;
    resmiFaturaNo?: string | null;
    karsiTarafFaturaNo?: string | null;
    iadeEdilenBelgeId?: number | null;
    muhasebeOnayinaGonderilmeTarihi?: string | null;
    muhasebeOnayTarihi?: string | null;
    faturaKesimTarihi?: string | null;
    musteriyeGonderimTarihi?: string | null;

    // İşlem yetenekleri — backend TicariBelgeIslemYetkisi'nden türetilir, burada yeniden ÜRETİLMEZ.
    guncellenebilirMi: boolean;
    silinebilirMi: boolean;
    muhasebeOnayinaGonderilebilirMi: boolean;
    iptalEdilebilirMi: boolean;
}

export interface TicariBelgeDetayDto extends TicariBelgeDto {
    satirlar: TicariBelgeSatirDto[];
}

export interface TicariBelgeFilterDto {
    tesisId?: number | null;
    belgeTipleri?: SatisBelgesiTipi[] | null;
    ticariDurum?: TicariBelgeDurumu | null;
    muhasebeDurumu?: TicariBelgeMuhasebeDurumu | null;
    faturalamaDurumu?: TicariBelgeFaturalamaDurumu | null;
    kaynakModul?: SatisKaynakModulu | null;
    kaynakTipi?: string | null;
    kaynakId?: string | null;
    belgeNo?: string | null;
    musteri?: string | null;
    baslangicTarihi?: string | null;
    bitisTarihi?: string | null;
}

export interface TicariBelgeGuncelleSatirRequest {
    siraNo: number;
    satirTipi: SatisBelgesiSatirTipi;
    aciklama: string;
    birim: string;
    miktar: number;
    birimFiyat: number;
    indirimOrani: number;
    indirimTutari: number;
    kdvUygulamaTipi: KdvUygulamaTipi;
    kdvIstisnaTanimId?: number | null;
    kdvOrani: number;
    tevkifatPay?: number | null;
    tevkifatPayda?: number | null;
    otvOrani: number;
    otvTutari: number;
    oivOrani: number;
    oivTutari: number;
    konaklamaVergisiOrani: number;
    konaklamaVergisiTutari: number;
    kaynakSatirId?: string | null;
}

export interface TicariBelgeGuncelleRequest {
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
    karsiTarafFaturaNo?: string | null;
    iadeEdilenBelgeId?: number | null;
    iadeEdilenBelgeReferansiKaldir: boolean;
    satirlar?: TicariBelgeGuncelleSatirRequest[] | null;
}

// ── Varsayılan değer yardımcıları ──

export function createDefaultTicariBelgeFilter(): TicariBelgeFilterDto {
    return {
        tesisId: null,
        belgeTipleri: null,
        ticariDurum: null,
        muhasebeDurumu: null,
        faturalamaDurumu: null,
        kaynakModul: null,
        kaynakTipi: null,
        kaynakId: null,
        belgeNo: null,
        musteri: null,
        baslangicTarihi: null,
        bitisTarihi: null
    };
}

export function createEmptyTicariBelgeGuncelleSatiri(): TicariBelgeGuncelleSatirRequest {
    return {
        siraNo: 1,
        satirTipi: SatisBelgesiSatirTipi.Diger,
        aciklama: '',
        birim: 'Adet',
        miktar: 1,
        birimFiyat: 0,
        indirimOrani: 0,
        indirimTutari: 0,
        kdvUygulamaTipi: KdvUygulamaTipi.Kdvli,
        kdvIstisnaTanimId: null,
        kdvOrani: 20,
        tevkifatPay: null,
        tevkifatPayda: null,
        otvOrani: 0,
        otvTutari: 0,
        oivOrani: 0,
        oivTutari: 0,
        konaklamaVergisiOrani: 0,
        konaklamaVergisiTutari: 0,
        kaynakSatirId: null
    };
}

export function satirDtoToGuncelleRequest(satir: TicariBelgeSatirDto): TicariBelgeGuncelleSatirRequest {
    return {
        siraNo: satir.siraNo,
        satirTipi: satir.satirTipi,
        aciklama: satir.aciklama,
        birim: satir.birim,
        miktar: satir.miktar,
        birimFiyat: satir.birimFiyat,
        indirimOrani: satir.indirimOrani,
        indirimTutari: satir.indirimTutari,
        kdvUygulamaTipi: satir.kdvUygulamaTipi,
        kdvIstisnaTanimId: satir.kdvIstisnaTanimId ?? null,
        kdvOrani: satir.kdvOrani,
        tevkifatPay: satir.tevkifatPay ?? null,
        tevkifatPayda: satir.tevkifatPayda ?? null,
        otvOrani: satir.otvOrani,
        otvTutari: satir.otvTutari,
        oivOrani: satir.oivOrani,
        oivTutari: satir.oivTutari,
        konaklamaVergisiOrani: satir.konaklamaVergisiOrani,
        konaklamaVergisiTutari: satir.konaklamaVergisiTutari,
        kaynakSatirId: satir.kaynakSatirId ?? null
    };
}

export function belgeToGuncelleRequest(belge: TicariBelgeDetayDto): TicariBelgeGuncelleRequest {
    return {
        belgeNo: belge.belgeNo,
        belgeTipi: belge.belgeTipi,
        tesisId: belge.tesisId ?? null,
        cariKartId: belge.cariKartId ?? null,
        belgeTarihi: belge.belgeTarihi ? toLocalDateString(new Date(belge.belgeTarihi)) : null,
        vadeTarihi: belge.vadeTarihi ? toLocalDateString(new Date(belge.vadeTarihi)) : null,
        musteriUnvan: belge.musteriUnvan ?? null,
        musteriAdSoyad: belge.musteriAdSoyad ?? null,
        musteriVergiNo: belge.musteriVergiNo ?? null,
        musteriTcKimlikNo: belge.musteriTcKimlikNo ?? null,
        musteriVergiDairesi: belge.musteriVergiDairesi ?? null,
        musteriAdres: belge.musteriAdres ?? null,
        musteriEposta: belge.musteriEposta ?? null,
        musteriTelefon: belge.musteriTelefon ?? null,
        kurumsalMi: belge.kurumsalMi,
        aciklama: belge.aciklama ?? null,
        karsiTarafFaturaNo: belge.karsiTarafFaturaNo ?? null,
        iadeEdilenBelgeId: belge.iadeEdilenBelgeId ?? null,
        iadeEdilenBelgeReferansiKaldir: false,
        satirlar: belge.satirlar.map(satirDtoToGuncelleRequest)
    };
}

export function getMusteriDisplayName(belge: TicariBelgeDto): string {
    if (belge.kurumsalMi) {
        return belge.musteriUnvan ?? belge.musteriAdSoyad ?? '-';
    }
    return belge.musteriAdSoyad ?? belge.musteriUnvan ?? '-';
}

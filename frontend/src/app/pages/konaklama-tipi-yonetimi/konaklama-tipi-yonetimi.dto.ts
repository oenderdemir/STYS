export interface KonaklamaTipiIcerikDto {
    hizmetKodu: string;
    hizmetAdi: string;
    miktar: number;
    periyot: string;
    periyotAdi: string;
    kullanimTipi: string;
    kullanimTipiAdi: string;
    kullanimNoktasi: string;
    kullanimNoktasiAdi: string;
    kullanimBaslangicSaati: string | null;
    kullanimBitisSaati: string | null;
    checkInGunuGecerliMi: boolean;
    checkOutGunuGecerliMi: boolean;
    aciklama: string | null;
}

export interface KonaklamaTipiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
    icerikKalemleri: KonaklamaTipiIcerikDto[];
}

export interface KonaklamaTipiTesisDto {
    id: number;
    ad: string;
}

export interface KonaklamaTipiTesisAtamaDto {
    konaklamaTipiId: number;
    kod: string;
    ad: string;
    globalAktifMi: boolean;
    tesisteKullanilabilirMi: boolean;
    overrideVarMi: boolean;
    etkinIcerikSayisi: number;
}

export interface KonaklamaTipiTesisIcerikOverrideDto {
    konaklamaTipiIcerikKalemiId: number;
    hizmetKodu: string;
    hizmetAdi: string;
    overrideVarMi: boolean;
    devreDisiMi: boolean;
    globalMiktar: number;
    globalPeriyot: string;
    globalPeriyotAdi: string;
    globalKullanimTipi: string;
    globalKullanimTipiAdi: string;
    globalKullanimNoktasi: string;
    globalKullanimNoktasiAdi: string;
    globalKullanimBaslangicSaati: string | null;
    globalKullanimBitisSaati: string | null;
    globalCheckInGunuGecerliMi: boolean;
    globalCheckOutGunuGecerliMi: boolean;
    globalAciklama: string | null;
    miktar: number;
    periyot: string;
    periyotAdi: string;
    kullanimTipi: string;
    kullanimTipiAdi: string;
    kullanimNoktasi: string;
    kullanimNoktasiAdi: string;
    kullanimBaslangicSaati: string | null;
    kullanimBitisSaati: string | null;
    checkInGunuGecerliMi: boolean;
    checkOutGunuGecerliMi: boolean;
    aciklama: string | null;
}

export interface KonaklamaTipiYonetimBaglamDto {
    globalTipYonetimiYapabilirMi: boolean;
    tesisler: KonaklamaTipiTesisDto[];
}

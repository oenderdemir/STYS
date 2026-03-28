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
}

export interface KonaklamaTipiYonetimBaglamDto {
    globalTipYonetimiYapabilirMi: boolean;
    tesisler: KonaklamaTipiTesisDto[];
}

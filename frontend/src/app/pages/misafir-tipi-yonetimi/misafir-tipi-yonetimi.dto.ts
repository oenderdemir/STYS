export interface MisafirTipiDto {
    id?: number | null;
    kod: string;
    ad: string;
    aktifMi: boolean;
}

export interface MisafirTipiTesisDto {
    id: number;
    ad: string;
}

export interface MisafirTipiYonetimBaglamDto {
    globalTipYonetimiYapabilirMi: boolean;
    tesisler: MisafirTipiTesisDto[];
}

export interface MisafirTipiTesisAtamaDto {
    misafirTipiId: number;
    kod: string;
    ad: string;
    globalAktifMi: boolean;
    tesisteKullanilabilirMi: boolean;
}

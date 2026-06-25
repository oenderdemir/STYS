export interface KurumModel {
    id: number;
    kod: string;
    ad: string;
    vergiNo?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aktifMi: boolean;
    logoDosyaAdi?: string | null;
    logoOrijinalDosyaAdi?: string | null;
    logoContentType?: string | null;
    logoBoyut?: number | null;
    logoYuklenmeTarihi?: string | null;
    logoUrl?: string | null;
    tenantKey?: string | null;
    loginHost?: string | null;
}

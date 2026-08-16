export interface KurumModel {
    id: number;
    kod: string;
    ad: string;
    vergiNo?: string | null;
    vergiDairesi?: string | null;
    adres?: string | null;
    ilce?: string | null;
    il?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aktifMi: boolean;
    /** When true, agents enrolling into this kurum always require central approval. */
    agentEnrollmentRequiresApproval?: boolean;
    logoDosyaAdi?: string | null;
    logoOrijinalDosyaAdi?: string | null;
    logoContentType?: string | null;
    logoBoyut?: number | null;
    logoYuklenmeTarihi?: string | null;
    logoUrl?: string | null;
    tenantKey?: string | null;
    loginHost?: string | null;
}

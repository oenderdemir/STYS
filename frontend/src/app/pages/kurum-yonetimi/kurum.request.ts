export interface CreateKurumRequest {
    kod: string;
    ad: string;
    vergiNo?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aktifMi: boolean;
    tenantKey?: string | null;
    loginHost?: string | null;
}

export interface UpdateKurumRequest extends CreateKurumRequest {}

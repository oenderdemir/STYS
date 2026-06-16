export interface CreateKurumRequest {
    kod: string;
    ad: string;
    vergiNo?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aktifMi: boolean;
}

export interface UpdateKurumRequest extends CreateKurumRequest {}

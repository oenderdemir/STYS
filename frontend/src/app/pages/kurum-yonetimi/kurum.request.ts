export interface CreateKurumRequest {
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
    agentEnrollmentRequiresApproval: boolean;
    tenantKey?: string | null;
    loginHost?: string | null;
}

export interface UpdateKurumRequest extends CreateKurumRequest {}

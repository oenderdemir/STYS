export interface KurumModel {
    id: number;
    kod: string;
    ad: string;
    vergiNo?: string | null;
    telefon?: string | null;
    eposta?: string | null;
    aktifMi: boolean;
}

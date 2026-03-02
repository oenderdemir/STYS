export interface TesisDto {
    id?: number | null;
    ad: string;
    ilId: number;
    telefon: string;
    adres: string;
    eposta?: string | null;
    aktifMi: boolean;
    yoneticiUserIds?: string[] | null;
    resepsiyonistUserIds?: string[] | null;
}

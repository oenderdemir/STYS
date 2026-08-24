export interface TesisDto {
    id?: number | null;
    ad: string;
    ilId: number;
    telefon: string;
    adres: string;
    eposta?: string | null;
    girisSaati: string;
    cikisSaati: string;
    ekHizmetPaketCakismaPolitikasi: string;
    stokCikisYontemi: string;
    aktifMi: boolean;
    yoneticiUserIds?: string[] | null;
    resepsiyonistUserIds?: string[] | null;
    muhasebeciUserIds?: string[] | null;
}

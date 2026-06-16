export interface UserKurumModel {
    id?: string | null;
    userId: string;
    kurumId: number;
    varsayilanMi: boolean;
    aktifMi: boolean;
    isKurumAdmin: boolean;
}

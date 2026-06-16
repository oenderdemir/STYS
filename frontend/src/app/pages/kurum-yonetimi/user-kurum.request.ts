export interface AssignUserKurumRequest {
    userId: string;
    kurumId: number;
    varsayilanMi: boolean;
    aktifMi: boolean;
    isKurumAdmin: boolean;
}

export interface UpdateUserKurumRequest {
    varsayilanMi: boolean;
    aktifMi: boolean;
    isKurumAdmin: boolean;
}

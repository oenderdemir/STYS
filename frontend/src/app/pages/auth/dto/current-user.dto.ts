export interface CurrentUserDto {
    userName: string | null;
    userStatus: string | null;
    defaultRoute: string | null;
    aktifKurumId: number | null;
    kurumIds: number[];
    kurumAdminKurumIds: number[];
    isKurumAdmin: boolean;
    isSuperAdmin: boolean;
}

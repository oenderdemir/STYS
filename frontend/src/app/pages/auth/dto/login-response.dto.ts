export interface LoginResponseDto {
    authenticateResult: boolean;
    authToken: string;
    accessTokenExpireDate: string;
    refreshToken?: string | null;
    refreshTokenExpireDate?: string | null;
    defaultRoute: string | null;
    userStatus: string | null;
    permissions: string[];
    aktifKurumId?: number | null;
    kurumIds?: number[];
    kurumAdminKurumIds?: number[];
    isKurumAdmin?: boolean;
    isSuperAdmin?: boolean;
}

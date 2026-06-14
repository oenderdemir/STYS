export interface LoginResponseDto {
    authenticateResult: boolean;
    authToken: string;
    accessTokenExpireDate: string;
    refreshToken: string;
    refreshTokenExpireDate: string | null;
    defaultRoute: string | null;
    userStatus: string | null;
    permissions: string[];
}

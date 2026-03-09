export interface LoginResponseDto {
    authenticateResult: boolean;
    authToken: string;
    accessTokenExpireDate: string;
    refreshToken: string;
    refreshTokenExpireDate: string | null;
    userStatus: string | null;
}

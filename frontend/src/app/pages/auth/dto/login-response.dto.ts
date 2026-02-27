export interface LoginResponseDto {
    authenticateResult: boolean;
    authToken: string;
    accessTokenExpireDate: string;
    userStatus: string | null;
}

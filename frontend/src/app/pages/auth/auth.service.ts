import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { LoginRequestDto, LoginResponseDto } from './dto';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly http = inject(HttpClient);
    private readonly tokenStorageKey = 'stys.auth.token';
    private readonly tokenExpiryStorageKey = 'stys.auth.token_expiry';
    private readonly apiBaseUrl = '/api';

    login(userName: string, password: string): Observable<LoginResponseDto> {
        const request: LoginRequestDto = {
            userName,
            password
        };

        return this.http.post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/auth/auth/login`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Login request failed.');
            })
        );
    }

    storeSession(response: LoginResponseDto): void {
        localStorage.setItem(this.tokenStorageKey, response.authToken);
        localStorage.setItem(this.tokenExpiryStorageKey, response.accessTokenExpireDate);
    }

    clearSession(): void {
        localStorage.removeItem(this.tokenStorageKey);
        localStorage.removeItem(this.tokenExpiryStorageKey);
    }

    getToken(): string | null {
        return localStorage.getItem(this.tokenStorageKey);
    }

    isAuthenticated(): boolean {
        const token = this.getToken();
        if (!token) {
            return false;
        }

        const expiryDateRaw = localStorage.getItem(this.tokenExpiryStorageKey);
        if (!expiryDateRaw) {
            return true;
        }

        const expiryDate = new Date(expiryDateRaw);
        if (Number.isNaN(expiryDate.getTime())) {
            this.clearSession();
            return false;
        }

        if (expiryDate.getTime() <= Date.now()) {
            this.clearSession();
            return false;
        }

        return true;
    }
}

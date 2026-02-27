import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl, getSessionInactivityTimeoutMs } from '../../core/config';
import { ChangePasswordRequestDto, LoginRequestDto, LoginResponseDto } from './dto';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly http = inject(HttpClient);
    private readonly router = inject(Router);
    private readonly tokenStorageKey = 'stys.auth.token';
    private readonly tokenExpiryStorageKey = 'stys.auth.token_expiry';
    private readonly userStatusStorageKey = 'stys.auth.user_status';
    private readonly inactivityTimeoutMs = getSessionInactivityTimeoutMs();
    private readonly apiBaseUrl = getApiBaseUrl();
    private inactivityTimeoutHandle: ReturnType<typeof setTimeout> | null = null;
    readonly sessionRevision = signal(0);

    constructor() {
        this.addActivityListeners();
        this.resetInactivityTimerIfNeeded();
    }

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

    changePassword(currentPassword: string, newPassword: string, newPassword2: string): Observable<LoginResponseDto> {
        const request: ChangePasswordRequestDto = {
            currentPassword,
            newPassword,
            newPassword2
        };

        return this.http.post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/auth/auth/changepassword`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Change password request failed.');
            })
        );
    }

    storeSession(response: LoginResponseDto): void {
        localStorage.setItem(this.tokenStorageKey, response.authToken);
        localStorage.setItem(this.tokenExpiryStorageKey, response.accessTokenExpireDate);
        if (response.userStatus && response.userStatus.trim().length > 0) {
            localStorage.setItem(this.userStatusStorageKey, response.userStatus.trim());
        } else {
            localStorage.removeItem(this.userStatusStorageKey);
        }
        this.resetInactivityTimer();
        this.bumpSessionRevision();
    }

    clearSession(): void {
        localStorage.removeItem(this.tokenStorageKey);
        localStorage.removeItem(this.tokenExpiryStorageKey);
        localStorage.removeItem(this.userStatusStorageKey);
        this.clearInactivityTimer();
        this.bumpSessionRevision();
    }

    logout(options?: { reason?: 'expired' | 'inactivity' | 'unauthorized'; preserveReturnUrl?: boolean }): void {
        const reason = options?.reason;
        const preserveReturnUrl = options?.preserveReturnUrl ?? true;
        const currentUrl = this.router.url;

        this.clearSession();

        const queryParams: Record<string, string> = {};
        if (reason) {
            queryParams['reason'] = reason;
        }
        if (preserveReturnUrl && currentUrl && !currentUrl.startsWith('/auth')) {
            queryParams['returnUrl'] = currentUrl;
        }

        if (Object.keys(queryParams).length > 0) {
            void this.router.navigate(['/auth/login'], { queryParams });
            return;
        }

        void this.router.navigate(['/auth/login']);
    }

    getToken(): string | null {
        return localStorage.getItem(this.tokenStorageKey);
    }

    getUserStatus(): string | null {
        const value = localStorage.getItem(this.userStatusStorageKey);
        if (!value || value.trim().length === 0) {
            return null;
        }

        return value.trim();
    }

    mustChangePassword(): boolean {
        const status = this.getUserStatus();
        if (!status) {
            return false;
        }

        return status.trim().toLowerCase() === 'mustchangepassword';
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

    getUserPermissions(): string[] {
        const token = this.getToken();
        if (!token) {
            return [];
        }

        const payload = this.decodeJwtPayload(token);
        if (!payload) {
            return [];
        }

        const permissions = [
            ...this.readClaimAsStringArray(payload, 'permission'),
            ...this.readClaimAsStringArray(payload, 'permissions')
        ];

        return [...new Set(permissions)];
    }

    hasPermission(permission: string): boolean {
        const permissions = this.getUserPermissions();
        return permissions.includes(permission) || permissions.includes('KullaniciTipi.Admin');
    }

    resetInactivityTimer(): void {
        if (!this.isAuthenticated()) {
            return;
        }

        this.clearInactivityTimer();
        this.inactivityTimeoutHandle = setTimeout(() => {
            this.logout({ reason: 'inactivity' });
        }, this.inactivityTimeoutMs);
    }

    private resetInactivityTimerIfNeeded(): void {
        if (this.isAuthenticated()) {
            this.resetInactivityTimer();
        }
    }

    private clearInactivityTimer(): void {
        if (this.inactivityTimeoutHandle === null) {
            return;
        }

        clearTimeout(this.inactivityTimeoutHandle);
        this.inactivityTimeoutHandle = null;
    }

    private addActivityListeners(): void {
        if (typeof document === 'undefined') {
            return;
        }

        const monitoredEvents: Array<keyof DocumentEventMap> = ['mousemove', 'keydown', 'click'];
        for (const eventName of monitoredEvents) {
            document.addEventListener(eventName, () => {
                this.resetInactivityTimerIfNeeded();
            });
        }
    }

    private decodeJwtPayload(token: string): Record<string, unknown> | null {
        const tokenParts = token.split('.');
        if (tokenParts.length < 2) {
            return null;
        }

        try {
            const encodedPayload = tokenParts[1].replace(/-/g, '+').replace(/_/g, '/');
            const paddedPayload = encodedPayload.padEnd(Math.ceil(encodedPayload.length / 4) * 4, '=');
            const decodedPayload = atob(paddedPayload);
            const parsedPayload = JSON.parse(decodedPayload);

            if (typeof parsedPayload !== 'object' || parsedPayload === null) {
                return null;
            }

            return parsedPayload as Record<string, unknown>;
        } catch {
            return null;
        }
    }

    private readClaimAsStringArray(payload: Record<string, unknown>, claimName: string): string[] {
        const claimValue = payload[claimName];
        if (typeof claimValue === 'string') {
            return [claimValue];
        }

        if (!Array.isArray(claimValue)) {
            return [];
        }

        return claimValue.filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
    }

    private bumpSessionRevision(): void {
        this.sessionRevision.update((value) => value + 1);
    }
}

import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl, getSessionInactivityTimeoutMs } from '../../core/config';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { ChangePasswordRequestDto, CurrentUserDto, LoginRequestDto, LoginResponseDto } from './dto';

interface SelectKurumRequestDto {
    kurumId: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly http = inject(HttpClient);
    private readonly router = inject(Router);
    private readonly tokenStorageKey = 'stys.auth.token';
    private readonly tokenExpiryStorageKey = 'stys.auth.token_expiry';
    private readonly userStatusStorageKey = 'stys.auth.user_status';
    private readonly defaultRouteStorageKey = 'stys.auth.default_route';
    private readonly permissionsStorageKey = 'stys.auth.permissions';
    private readonly activeKurumIdStorageKey = 'stys.auth.active_kurum_id';
    private readonly kurumIdsStorageKey = 'stys.auth.kurum_ids';
    private readonly kurumAdminKurumIdsStorageKey = 'stys.auth.kurum_admin_kurum_ids';
    private readonly isKurumAdminStorageKey = 'stys.auth.is_kurum_admin';
    private readonly isSuperAdminStorageKey = 'stys.auth.is_super_admin';
    private readonly inactivityTimeoutMs = getSessionInactivityTimeoutMs();
    private readonly apiBaseUrl = getApiBaseUrl();
    private inactivityTimeoutHandle: ReturnType<typeof setTimeout> | null = null;
    private refreshInFlight$: Observable<LoginResponseDto> | null = null;
    private readonly muhasebeTesisContext = inject(MuhasebeTesisContextService);
    readonly aktifKurumId = signal<number | null>(this.readStoredNumber(this.activeKurumIdStorageKey));
    readonly kurumIds = signal<number[]>(this.readStoredNumberArray(this.kurumIdsStorageKey));
    readonly kurumAdminKurumIds = signal<number[]>(this.readStoredNumberArray(this.kurumAdminKurumIdsStorageKey));
    readonly isKurumAdmin = signal(this.readStoredBoolean(this.isKurumAdminStorageKey));
    readonly isSuperAdmin = signal(this.readStoredBoolean(this.isSuperAdminStorageKey));
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

        return this.http.post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/auth/auth/login`, request, { withCredentials: true }).pipe(
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

        return this.http.post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/auth/auth/changepassword`, request, { withCredentials: true }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Change password request failed.');
            })
        );
    }

    refreshSession(): Observable<LoginResponseDto> {
        if (this.refreshInFlight$) {
            return this.refreshInFlight$;
        }

        const activeKurumId = this.getAktifKurumId();
        const body = activeKurumId !== null ? { kurumId: activeKurumId } : {};

        this.refreshInFlight$ = this.http
            .post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/auth/auth/refresh`, body, { withCredentials: true })
            .pipe(
                map((responseEnvelope) => {
                    if (responseEnvelope.success && responseEnvelope.data) {
                        return this.normalizeLoginResponse(responseEnvelope.data);
                    }

                    throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Refresh token request failed.');
                }),
                tap((response) => this.storeSession(response)),
                finalize(() => {
                    this.refreshInFlight$ = null;
                }),
                shareReplay(1)
            );

        return this.refreshInFlight$;
    }

    selectKurum(kurumId: number): Observable<LoginResponseDto> {
        const request: SelectKurumRequestDto = {
            kurumId
        };

        return this.http.post<ApiResponse<LoginResponseDto>>(`${this.apiBaseUrl}/api/auth/select-kurum`, request, { withCredentials: true }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeLoginResponse(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kurum degistirme islemi basarisiz oldu.');
            }),
            tap((response) => this.storeSession(response))
        );
    }

    storeSession(response: LoginResponseDto): void {
        const normalized = this.normalizeLoginResponse(response);
        localStorage.setItem(this.tokenStorageKey, normalized.authToken);
        localStorage.setItem(this.tokenExpiryStorageKey, normalized.accessTokenExpireDate);
        this.storePermissions(normalized.permissions);
        const defaultRoute = this.normalizeRoute(normalized.defaultRoute);
        if (defaultRoute) {
            localStorage.setItem(this.defaultRouteStorageKey, defaultRoute);
        } else {
            localStorage.removeItem(this.defaultRouteStorageKey);
        }
        if (normalized.userStatus && normalized.userStatus.trim().length > 0) {
            localStorage.setItem(this.userStatusStorageKey, normalized.userStatus.trim());
        } else {
            localStorage.removeItem(this.userStatusStorageKey);
        }
        this.storeNumber(this.activeKurumIdStorageKey, normalized.aktifKurumId);
        this.storeNumberArray(this.kurumIdsStorageKey, normalized.kurumIds);
        this.storeNumberArray(this.kurumAdminKurumIdsStorageKey, normalized.kurumAdminKurumIds);
        this.storeBoolean(this.isKurumAdminStorageKey, normalized.isKurumAdmin);
        this.storeBoolean(this.isSuperAdminStorageKey, normalized.isSuperAdmin);
        this.resetInactivityTimer();
        this.bumpSessionRevision();
    }

    clearSession(): void {
        localStorage.removeItem(this.tokenStorageKey);
        localStorage.removeItem(this.tokenExpiryStorageKey);
        localStorage.removeItem(this.defaultRouteStorageKey);
        localStorage.removeItem(this.userStatusStorageKey);
        localStorage.removeItem(this.permissionsStorageKey);
        localStorage.removeItem(this.activeKurumIdStorageKey);
        localStorage.removeItem(this.kurumIdsStorageKey);
        localStorage.removeItem(this.kurumAdminKurumIdsStorageKey);
        localStorage.removeItem(this.isKurumAdminStorageKey);
        localStorage.removeItem(this.isSuperAdminStorageKey);
        this.aktifKurumId.set(null);
        this.kurumIds.set([]);
        this.kurumAdminKurumIds.set([]);
        this.isKurumAdmin.set(false);
        this.isSuperAdmin.set(false);
        this.refreshInFlight$ = null;
        this.muhasebeTesisContext.clearPersistedTesis();
        this.clearInactivityTimer();
        this.bumpSessionRevision();
    }

    logout(options?: { reason?: 'expired' | 'inactivity' | 'unauthorized'; preserveReturnUrl?: boolean }): void {
        const reason = options?.reason;
        const preserveReturnUrl = options?.preserveReturnUrl ?? true;
        const currentUrl = this.router.url;
        this.http
            .post(`${this.apiBaseUrl}/auth/auth/logout`, {}, { withCredentials: true })
            .pipe(catchError(() => of(null)))
            .subscribe();

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

    getDefaultRoute(): string | null {
        const value = localStorage.getItem(this.defaultRouteStorageKey);
        return this.normalizeRoute(value);
    }

    getAktifKurumId(): number | null {
        return this.aktifKurumId();
    }

    getKurumIds(): number[] {
        return [...this.kurumIds()];
    }

    getKurumAdminKurumIds(): number[] {
        return [...this.kurumAdminKurumIds()];
    }

    isSuperAdminUser(): boolean {
        return this.isSuperAdmin();
    }

    isKurumAdminUser(): boolean {
        return this.isKurumAdmin();
    }

    isKurumAdminFor(kurumId: number): boolean {
        if (this.isSuperAdmin()) {
            return true;
        }

        return this.kurumAdminKurumIds().includes(kurumId);
    }

    getCurrentUserSnapshot(): CurrentUserDto | null {
        if (!this.isAuthenticated()) {
            return null;
        }

        return {
            userName: this.getCurrentUserName(),
            userStatus: this.getUserStatus(),
            defaultRoute: this.getDefaultRoute(),
            aktifKurumId: this.getAktifKurumId(),
            kurumIds: this.getKurumIds(),
            kurumAdminKurumIds: this.getKurumAdminKurumIds(),
            isKurumAdmin: this.isKurumAdminUser(),
            isSuperAdmin: this.isSuperAdminUser()
        };
    }

    getLandingRoute(fallbackRoute: string = '/'): string {
        return this.getDefaultRoute() ?? fallbackRoute;
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
        const storedPermissions = this.readStoredPermissions();
        if (storedPermissions.length > 0) {
            return storedPermissions;
        }

        return this.getPermissionsFromToken();
    }

    hasPermission(permission: string): boolean {
        const permissions = this.getUserPermissions();
        if (permissions.includes(permission)) {
            return true;
        }

        if (permission.endsWith('.View')) {
            const managePermission = `${permission.slice(0, -'.View'.length)}.Manage`;
            return permissions.includes(managePermission);
        }

        return false;
    }

    getCurrentUserName(): string | null {
        const token = this.getToken();
        if (!token) {
            return null;
        }

        const payload = this.decodeJwtPayload(token);
        if (!payload) {
            return null;
        }

        const userName = this.readClaimAsString(payload, 'userName');
        if (userName) {
            return userName;
        }

        const nameIdentifier = this.readClaimAsString(payload, 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier');
        if (nameIdentifier) {
            return nameIdentifier;
        }

        const uniqueName = this.readClaimAsString(payload, 'unique_name');
        if (uniqueName) {
            return uniqueName;
        }

        return null;
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

    private normalizeLoginResponse(response: LoginResponseDto): LoginResponseDto {
        return {
            ...response,
            refreshToken: response.refreshToken ?? '',
            refreshTokenExpireDate: response.refreshTokenExpireDate ?? null,
            permissions: Array.isArray(response.permissions) ? response.permissions : [],
            kurumIds: this.normalizeNumberList(response.kurumIds),
            kurumAdminKurumIds: this.normalizeNumberList(response.kurumAdminKurumIds),
            isKurumAdmin: response.isKurumAdmin ?? false,
            isSuperAdmin: response.isSuperAdmin ?? false,
            aktifKurumId: response.aktifKurumId ?? null
        };
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

    private getPermissionsFromToken(): string[] {
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

    private storePermissions(permissions: string[] | null | undefined): void {
        const normalizedPermissions = this.normalizePermissionList(permissions);
        if (normalizedPermissions.length === 0) {
            localStorage.removeItem(this.permissionsStorageKey);
            return;
        }

        localStorage.setItem(this.permissionsStorageKey, JSON.stringify(normalizedPermissions));
    }

    private storeNumber(storageKey: string, value: number | null | undefined): void {
        if (value === null || value === undefined || !Number.isFinite(value)) {
            localStorage.removeItem(storageKey);
            if (storageKey === this.activeKurumIdStorageKey) {
                this.aktifKurumId.set(null);
            }
            return;
        }

        const normalizedValue = Math.trunc(value);
        localStorage.setItem(storageKey, normalizedValue.toString());
        if (storageKey === this.activeKurumIdStorageKey) {
            this.aktifKurumId.set(normalizedValue);
        }
    }

    private storeNumberArray(storageKey: string, values: number[] | null | undefined): void {
        const normalizedValues = this.normalizeNumberList(values);
        if (normalizedValues.length === 0) {
            localStorage.removeItem(storageKey);
            if (storageKey === this.kurumIdsStorageKey) {
                this.kurumIds.set([]);
            } else if (storageKey === this.kurumAdminKurumIdsStorageKey) {
                this.kurumAdminKurumIds.set([]);
            }
            return;
        }

        localStorage.setItem(storageKey, JSON.stringify(normalizedValues));
        if (storageKey === this.kurumIdsStorageKey) {
            this.kurumIds.set(normalizedValues);
        } else if (storageKey === this.kurumAdminKurumIdsStorageKey) {
            this.kurumAdminKurumIds.set(normalizedValues);
        }
    }

    private storeBoolean(storageKey: string, value: boolean | null | undefined): void {
        const normalizedValue = value === true;
        localStorage.setItem(storageKey, JSON.stringify(normalizedValue));
        if (storageKey === this.isKurumAdminStorageKey) {
            this.isKurumAdmin.set(normalizedValue);
        } else if (storageKey === this.isSuperAdminStorageKey) {
            this.isSuperAdmin.set(normalizedValue);
        }
    }

    private readStoredNumber(storageKey: string): number | null {
        const rawValue = localStorage.getItem(storageKey);
        if (!rawValue || rawValue.trim().length === 0) {
            return null;
        }

        const parsed = Number(rawValue);
        return Number.isFinite(parsed) ? Math.trunc(parsed) : null;
    }

    private readStoredNumberArray(storageKey: string): number[] {
        const rawValue = localStorage.getItem(storageKey);
        if (!rawValue || rawValue.trim().length === 0) {
            return [];
        }

        try {
            const parsed = JSON.parse(rawValue);
            if (!Array.isArray(parsed)) {
                return [];
            }

            return this.normalizeNumberList(parsed);
        } catch {
            return [];
        }
    }

    private readStoredBoolean(storageKey: string): boolean {
        const rawValue = localStorage.getItem(storageKey);
        if (!rawValue || rawValue.trim().length === 0) {
            return false;
        }

        try {
            const parsed = JSON.parse(rawValue);
            return parsed === true;
        } catch {
            return rawValue.trim().toLowerCase() === 'true';
        }
    }

    private readStoredPermissions(): string[] {
        const rawPermissions = localStorage.getItem(this.permissionsStorageKey);
        if (!rawPermissions || rawPermissions.trim().length === 0) {
            return [];
        }

        try {
            const parsed = JSON.parse(rawPermissions);
            if (!Array.isArray(parsed)) {
                return [];
            }

            return this.normalizePermissionList(parsed.filter((value): value is string => typeof value === 'string'));
        } catch {
            return [];
        }
    }

    private normalizePermissionList(permissions: string[] | null | undefined): string[] {
        if (!permissions || permissions.length === 0) {
            return [];
        }

        return [...new Set(permissions.map((permission) => permission.trim()).filter((permission) => permission.length > 0))];
    }

    private normalizeNumberList(values: number[] | null | undefined): number[] {
        if (!values || values.length === 0) {
            return [];
        }

        return [...new Set(values.map((value) => Math.trunc(value)).filter((value) => Number.isFinite(value)))];
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

    private readClaimAsString(payload: Record<string, unknown>, claimName: string): string | null {
        const claimValue = payload[claimName];
        if (typeof claimValue !== 'string') {
            return null;
        }

        const normalizedValue = claimValue.trim();
        return normalizedValue.length > 0 ? normalizedValue : null;
    }

    private normalizeRoute(route: string | null | undefined): string | null {
        if (!route || route.trim().length === 0) {
            return null;
        }

        const normalizedRoute = route.trim();
        if (!normalizedRoute.startsWith('/')) {
            return null;
        }

        return normalizedRoute;
    }

    private bumpSessionRevision(): void {
        this.sessionRevision.update((value) => value + 1);
    }
}

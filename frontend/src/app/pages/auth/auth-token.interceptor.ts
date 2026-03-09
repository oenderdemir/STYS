import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { getApiBaseUrl } from '../../core/config';
import { AuthService } from './auth.service';

export const authTokenInterceptor: HttpInterceptorFn = (request, next) => {
    const authService = inject(AuthService);
    const apiBaseUrl = getApiBaseUrl();
    const normalizedUrl = request.url.toLowerCase();
    const isBackendRequest = isBackendApiRequest(normalizedUrl, apiBaseUrl);
    const isAuthRequest = normalizedUrl.includes('/auth/auth/login');
    const isRefreshRequest = normalizedUrl.includes('/auth/auth/refresh');
    const isLogoutRequest = normalizedUrl.includes('/auth/auth/logout');
    const isChangePasswordRequest = normalizedUrl.includes('/auth/auth/changepassword');
    const hasRetryMarker = request.headers.has('x-auth-refreshed');
    const token = authService.getToken();

    if (isBackendRequest && !isAuthRequest && !isRefreshRequest && !isLogoutRequest && !token) {
        authService.logout({ reason: 'unauthorized', preserveReturnUrl: false });
        return throwError(() =>
            new HttpErrorResponse({
                status: 401,
                statusText: 'Unauthorized',
                url: request.url,
                error: {
                    message: 'Authentication token is required.'
                }
            })
        );
    }

    const requestWithAuthorization =
        !isAuthRequest && !isRefreshRequest && token && !request.headers.has('Authorization')
            ? request.clone({
                  setHeaders: {
                      Authorization: `Bearer ${token}`
                  }
              })
            : request;

    return next(requestWithAuthorization).pipe(
        catchError((error: unknown) => {
            if (error instanceof HttpErrorResponse && !isAuthRequest && !isRefreshRequest && !isLogoutRequest && authService.getToken()) {
                const isUnauthorized = error.status === 401;
                const shouldAutoLogout = !isChangePasswordRequest;
                if (isUnauthorized && shouldAutoLogout && !hasRetryMarker) {
                    const retryRequest = requestWithAuthorization.clone({
                        setHeaders: {
                            'x-auth-refreshed': '1'
                        }
                    });

                    return authService.refreshSession().pipe(
                        switchMap((response) => {
                            const retryToken = response.authToken || authService.getToken();
                            const retriedWithToken =
                                retryToken && !retryRequest.headers.has('Authorization')
                                    ? retryRequest.clone({
                                          setHeaders: {
                                              Authorization: `Bearer ${retryToken}`
                                          }
                                      })
                                    : retryRequest;

                            return next(retriedWithToken);
                        }),
                        catchError((refreshError: unknown) => {
                            authService.logout({ reason: looksLikeExpiredToken(error) ? 'expired' : 'unauthorized' });
                            return throwError(() => refreshError);
                        })
                    );
                }

                if (isUnauthorized && shouldAutoLogout) {
                    authService.logout({ reason: looksLikeExpiredToken(error) ? 'expired' : 'unauthorized' });
                }
            }

            return throwError(() => error);
        })
    );
};

function isBackendApiRequest(normalizedRequestUrl: string, apiBaseUrl: string): boolean {
    if (normalizedRequestUrl.includes('/ui/') || normalizedRequestUrl.includes('/auth/')) {
        return true;
    }

    const normalizedApiBaseUrl = apiBaseUrl.trim().toLowerCase().replace(/\/+$/, '');
    if (!normalizedApiBaseUrl) {
        return normalizedRequestUrl.startsWith('/api');
    }

    if (normalizedApiBaseUrl.startsWith('/')) {
        return normalizedRequestUrl.startsWith(normalizedApiBaseUrl);
    }

    return normalizedRequestUrl.startsWith(`${normalizedApiBaseUrl}/`) || normalizedRequestUrl === normalizedApiBaseUrl;
}

function looksLikeExpiredToken(error: HttpErrorResponse): boolean {
    const authenticateHeader = error.headers?.get('WWW-Authenticate') ?? error.headers?.get('Www-Authenticate') ?? '';
    const headerContent = authenticateHeader.toLowerCase();

    const errorContent = typeof error.error === 'string' ? error.error : JSON.stringify(error.error ?? {});
    const bodyContent = errorContent.toLowerCase();

    return (headerContent.includes('invalid_token') && headerContent.includes('expired')) || bodyContent.includes('token expired') || bodyContent.includes('expired');
}

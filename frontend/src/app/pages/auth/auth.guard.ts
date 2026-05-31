import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isAuthenticated()) {
        if (authService.mustChangePassword()) {
            if (state.url !== '/') {
                return router.createUrlTree(['/']);
            }

            return true;
        }

        if (state.url === '/' || state.url === '') {
            const defaultRoute = authService.getDefaultRoute();
            if (defaultRoute) {
                return router.createUrlTree([defaultRoute]);
            }
        }

        return true;
    }

    return router.createUrlTree(['/auth/login'], {
        queryParams: {
            returnUrl: state.url
        }
    });
};

export const authChildGuard: CanActivateChildFn = (_route, state) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isAuthenticated()) {
        if (authService.mustChangePassword()) {
            if (state.url !== '/') {
                return router.createUrlTree(['/']);
            }

            return true;
        }

        if (state.url === '/' || state.url === '') {
            const defaultRoute = authService.getDefaultRoute();
            if (defaultRoute) {
                return router.createUrlTree([defaultRoute]);
            }
        }

        return true;
    }

    return router.createUrlTree(['/auth/login'], {
        queryParams: {
            returnUrl: state.url
        }
    });
};

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Yeniden kullanılabilir functional permission guard. Kullanım:
 *   canActivate: [permissionGuard('TicariBelgeYonetimi.View')]
 *
 * authGuard/authChildGuard'ın YERİNİ ALMAZ - kimlik doğrulama kontrolü ayrıca uygulanmalıdır
 * (route ağacındaki üst düzey canActivate/canActivateChild zaten bunu sağlar). Bu guard yalnızca
 * yetki (permission) kontrolü ekler. Menu yetkisi (ör. "X.Menu") burada GEÇERLİ SAYILMAZ -
 * yalnızca View/Manage gibi asıl işlem yetkileri kabul edilir (AuthService.hasPermission zaten
 * ".View" için karşılık gelen ".Manage" yetkisini de otomatik kabul eder).
 *
 * Yetkisiz (ama kimliği doğrulanmış) kullanıcı, mevcut uygulamanın "varsayılan rota" davranışıyla
 * (AuthService.getLandingRoute) yönlendirilir - authGuard'ın "/" için kullandığı AYNI mekanizma.
 */
export function permissionGuard(permission: string): CanActivateFn {
    return () => {
        const authService = inject(AuthService);
        const router = inject(Router);

        if (authService.hasPermission(permission)) {
            return true;
        }

        return router.createUrlTree([authService.getLandingRoute()]);
    };
}

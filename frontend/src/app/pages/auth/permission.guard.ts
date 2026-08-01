import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

const GUVENLI_KOK_ROTA = '/';

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
 *
 * Yönlendirme döngüsü koruması: kullanıcının defaultRoute'u tam olarak erişmeye çalıştığı
 * (yetkisiz) URL'yle AYNIYSA, oraya TEKRAR yönlendirmek ya sonsuz bir döngü ya da Angular'ın
 * "aynı URL'ye navigasyon iptal edildi" davranışıyla sessiz bir no-op'a yol açar. Bu durumda
 * bunun yerine uygulamanın güvenli kök rotasına ("/") düşülür; kök rota da hedefle AYNIYSA
 * (örn. defaultRoute yapılandırması bozuksa) döngüyü kesin olarak kırmak için navigasyon
 * tamamen ENGELLENİR (false).
 */
export function permissionGuard(permission: string): CanActivateFn {
    return (_route, state) => {
        const authService = inject(AuthService);
        const router = inject(Router);

        if (authService.hasPermission(permission)) {
            return true;
        }

        const hedefUrl = state.url;
        const defaultRoute = authService.getLandingRoute();

        if (defaultRoute === hedefUrl) {
            return GUVENLI_KOK_ROTA === hedefUrl ? false : router.createUrlTree([GUVENLI_KOK_ROTA]);
        }

        return router.createUrlTree([defaultRoute]);
    };
}

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Döngü YARATMAYAN güvenli fallback rota. Uygulamada özel bir "erişim reddedildi" sayfası
 * bulunmuyor; en yakın uygun aday '/notfound' - app.routes.ts'te authGuard/authChildGuard'ın
 * (ve dolayısıyla herhangi bir permission/defaultRoute mantığının) DIŞINDA, üst seviyede,
 * guard'sız tanımlı tek genel-amaçlı sayfa budur (bkz. app.routes.ts: 'notfound' rotası
 * AppLayout/authGuard alt ağacına DAHİL DEĞİLDİR). "/" KASTEN kullanılmaz: authGuard, "/"e
 * yapılan HER navigasyonu (kullanıcı yetkili de olsa) otomatik olarak authService.getDefaultRoute()
 * rotasına yönlendirir (bkz. auth.guard.ts satır 18-23) - bu, permissionGuard "/"e düşürdüğünde
 * authGuard'ın onu doğrudan defaultRoute'a (yani kullanıcının zaten yetkisiz olduğu, reddedildiği
 * ASIL hedefe) geri göndermesine, dolayısıyla sonsuz bir yönlendirme döngüsüne yol açar.
 */
const GUVENLI_FALLBACK_ROTA = '/notfound';

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
 * "/" YERİNE (bkz. GUVENLI_FALLBACK_ROTA doc'u - "/" authGuard tarafından defaultRoute'a geri
 * yönlendirilip döngüyü YENİDEN oluşturur) '/notfound'a düşülür; O da (teorik olarak,
 * defaultRoute yanlışlıkla '/notfound' olarak yapılandırılmışsa) hedefle AYNIYSA döngüyü kesin
 * olarak kırmak için navigasyon tamamen ENGELLENİR (false).
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
            return GUVENLI_FALLBACK_ROTA === hedefUrl ? false : router.createUrlTree([GUVENLI_FALLBACK_ROTA]);
        }

        return router.createUrlTree([defaultRoute]);
    };
}

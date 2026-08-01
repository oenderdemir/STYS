import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { permissionGuard } from './permission.guard';

describe('permissionGuard', () => {
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(() => {
        authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['hasPermission', 'getLandingRoute']);
        routerSpy = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);
        routerSpy.createUrlTree.and.callFake(commands => ({ __urlTree: commands }) as unknown as UrlTree);

        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useValue: authServiceSpy },
                { provide: Router, useValue: routerSpy }
            ]
        });
    });

    function runGuard(stateUrl: string) {
        return TestBed.runInInjectionContext(() =>
            permissionGuard('TicariBelgeYonetimi.View')({} as never, { url: stateUrl } as never)
        );
    }

    it('yetkili kullanıcı için erişime izin verir', () => {
        authServiceSpy.hasPermission.and.returnValue(true);

        const result = runGuard('/ticari-belgeler');

        expect(result).toBeTrue();
        expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
    });

    it('yetkisiz kullanıcıyı defaultRoute farklıysa defaultRoute\'a yönlendirir', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/dashboard');

        runGuard('/ticari-belgeler');

        expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
    });

    it('defaultRoute erişilmeye çalışılan URL ile AYNIYSA döngü oluşturmadan güvenli kök rotaya (/) düşer', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/ticari-belgeler');

        runGuard('/ticari-belgeler');

        expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/']);
        expect(routerSpy.createUrlTree).not.toHaveBeenCalledWith(['/ticari-belgeler']);
    });

    it('güvenli kök rota da hedefle AYNIYSA sonsuz döngü yerine navigasyonu engeller (false)', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/');

        const result = runGuard('/');

        expect(result).toBeFalse();
        expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
    });
});

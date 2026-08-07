import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService } from './auth.service';
import { permissionGuard, permissionOrSuperAdminGuard } from './permission.guard';

describe('permissionGuard', () => {
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(() => {
        authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['hasPermission', 'getLandingRoute', 'isSuperAdminUser']);
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

    it('defaultRoute erişilmeye çalışılan URL ile AYNIYSA döngü oluşturmadan güvenli fallback rotaya (/notfound) düşer', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/ticari-belgeler');

        runGuard('/ticari-belgeler');

        expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/notfound']);
        expect(routerSpy.createUrlTree).not.toHaveBeenCalledWith(['/ticari-belgeler']);
    });

    it('"/" fallback OLARAK KULLANILMAZ - authGuard "/"e yapılan navigasyonu defaultRoute\'a geri yönlendirip döngüyü YENİDEN oluşturur', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/ticari-belgeler');

        runGuard('/ticari-belgeler');

        expect(routerSpy.createUrlTree).not.toHaveBeenCalledWith(['/']);
    });

    it('güvenli fallback rota da hedefle AYNIYSA sonsuz döngü yerine navigasyonu engeller (false)', () => {
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/notfound');

        const result = runGuard('/notfound');

        expect(result).toBeFalse();
        expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
    });
});

/** Faz 2B.11.1 görev md.13 - backend'in `View/Manage OR SuperAdmin` sözleşmesini birebir taşıyan hedefli guard'ın testleri. */
describe('permissionOrSuperAdminGuard', () => {
    let authServiceSpy: jasmine.SpyObj<AuthService>;
    let routerSpy: jasmine.SpyObj<Router>;

    beforeEach(() => {
        authServiceSpy = jasmine.createSpyObj<AuthService>('AuthService', ['hasPermission', 'getLandingRoute', 'isSuperAdminUser']);
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
            permissionOrSuperAdminGuard('MuhasebeSatisBelgeleriYonetimi.View')({} as never, { url: stateUrl } as never)
        );
    }

    it('SuperAdmin domain izne SAHIP OLMASA bile erisime izin verir', () => {
        authServiceSpy.isSuperAdminUser.and.returnValue(true);
        authServiceSpy.hasPermission.and.returnValue(false);

        const result = runGuard('/muhasebe/e-belge-yonetimi');

        expect(result).toBeTrue();
        expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
    });

    it('domain izni olan normal kullanici icin erisime izin verir', () => {
        authServiceSpy.isSuperAdminUser.and.returnValue(false);
        authServiceSpy.hasPermission.and.returnValue(true);

        const result = runGuard('/muhasebe/e-belge-yonetimi');

        expect(result).toBeTrue();
        expect(routerSpy.createUrlTree).not.toHaveBeenCalled();
    });

    it('ne SuperAdmin ne domain izni varsa defaultRoute a yonlendirir', () => {
        authServiceSpy.isSuperAdminUser.and.returnValue(false);
        authServiceSpy.hasPermission.and.returnValue(false);
        authServiceSpy.getLandingRoute.and.returnValue('/dashboard');

        runGuard('/muhasebe/e-belge-yonetimi');

        expect(routerSpy.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
    });
});

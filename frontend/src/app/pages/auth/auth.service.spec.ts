import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { LoginResponseDto } from './dto';
import { AuthService } from './auth.service';

function ornekLoginResponse(overrides?: Partial<LoginResponseDto>): LoginResponseDto {
    return {
        authenticateResult: true,
        authToken: 'header.payload.signature',
        accessTokenExpireDate: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
        refreshToken: 'refresh',
        refreshTokenExpireDate: null,
        defaultRoute: null,
        userStatus: null,
        permissions: [],
        aktifKurumId: 1,
        kurumIds: [1],
        kurumAdminKurumIds: [],
        isKurumAdmin: false,
        isSuperAdmin: false,
        ...overrides
    };
}

describe('AuthService - hareketsizlik uyarisi', () => {
    let service: AuthService;
    let httpMock: HttpTestingController;
    let routerSpy: jasmine.SpyObj<Router>;

    // Test ortaminda window.__env tanimli olmadigi icin varsayilan degerler kullanilir:
    // sessionInactivityTimeoutMs=600000 (10 dk), sessionInactivityWarningMs=120000 (2 dk).
    const timeoutMs = 600000;
    const warningMs = 120000;
    const warningDelayMs = timeoutMs - warningMs;

    beforeEach(() => {
        routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate']);
        Object.defineProperty(routerSpy, 'url', { value: '/rezervasyon-yonetimi', configurable: true });

        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [
                { provide: Router, useValue: routerSpy },
                { provide: MuhasebeTesisContextService, useValue: { clearPersistedTesis: () => undefined } }
            ]
        });

        service = TestBed.inject(AuthService);
        httpMock = TestBed.inject(HttpTestingController);
        jasmine.clock().install();
    });

    afterEach(() => {
        jasmine.clock().uninstall();
        httpMock.verify();
    });

    it('oturum acildiktan sonra hareketsizlik penceresinin sonuna dogru (timeout - warningMs) uyari gosterilir', () => {
        service.storeSession(ornekLoginResponse());

        expect(service.showInactivityWarning()).toBeFalse();

        jasmine.clock().tick(warningDelayMs + 10);

        expect(service.showInactivityWarning()).toBeTrue();
        expect(service.inactivityWarningSecondsRemaining()).toBeGreaterThan(0);
    });

    it('uyari gosterildikten sonra kullanici hicbir sey yapmazsa tam sure sonunda otomatik cikis yapilir', () => {
        service.storeSession(ornekLoginResponse());
        jasmine.clock().tick(warningDelayMs + 10);
        expect(service.showInactivityWarning()).toBeTrue();

        jasmine.clock().tick(timeoutMs - warningDelayMs);

        httpMock.expectOne((req) => req.url.includes('/auth/auth/logout')).flush({});
        expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login'], jasmine.objectContaining({ queryParams: jasmine.objectContaining({ reason: 'inactivity' }) }));
    });

    it('extendSession (ek sure istiyorum) cagrildiginda uyari kapanir ve hareketsizlik penceresi bastan baslar', () => {
        service.storeSession(ornekLoginResponse());
        jasmine.clock().tick(warningDelayMs + 10);
        expect(service.showInactivityWarning()).toBeTrue();

        service.extendSession();

        expect(service.showInactivityWarning()).toBeFalse();

        // extendSession() ANDAN itibaren yeni uyari esigi (warningDelayMs) ve yeni cikis ani
        // (timeoutMs) BASTAN sayilir. Once bu araligin bir kismini (eski esigin gecmis olacagi
        // kadar) ilerlet - uyari HENUZ gosterilmemis olmali.
        const ilkAdimMs = timeoutMs - warningDelayMs; // = warningMs
        jasmine.clock().tick(ilkAdimMs);
        expect(routerSpy.navigate).not.toHaveBeenCalled();
        expect(service.showInactivityWarning()).toBeFalse();

        // Yeni uyari esigine (extend anindan itibaren warningDelayMs) ULASMADAN hemen once hala
        // gizli, esigi GECINCE (ama yeni cikis anindan - extend anindan itibaren timeoutMs - cok
        // once) tekrar gosterilmelidir.
        const yeniEsigeKalanMs = warningDelayMs - ilkAdimMs;
        jasmine.clock().tick(yeniEsigeKalanMs - 1000);
        expect(service.showInactivityWarning()).toBeFalse();
        jasmine.clock().tick(1000);
        expect(service.showInactivityWarning()).toBeTrue();
        expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('uyari gosterilirken pasif aktivite (mousemove) sessizce oturumu uzatmaz - uyari acik kalir', () => {
        service.storeSession(ornekLoginResponse());
        jasmine.clock().tick(warningDelayMs + 10);
        expect(service.showInactivityWarning()).toBeTrue();

        document.dispatchEvent(new MouseEvent('mousemove'));

        expect(service.showInactivityWarning()).toBeTrue();

        // Pasif aktivite sayaci SIFIRLAMADIGI icin, kalan orijinal sure sonunda hala cikis yapilir.
        jasmine.clock().tick(timeoutMs - warningDelayMs);
        httpMock.expectOne((req) => req.url.includes('/auth/auth/logout')).flush({});
        expect(routerSpy.navigate).toHaveBeenCalled();
    });

    it('uyari gosterilmeden ONCE pasif aktivite hareketsizlik penceresini normal sekilde sifirlar', () => {
        service.storeSession(ornekLoginResponse());

        jasmine.clock().tick(warningDelayMs - 1000);
        document.dispatchEvent(new MouseEvent('mousemove'));

        // Aktivite sayaci sifirlandigi icin, ESKI zamanlamaya gore uyarinin gosterilecegi an gelmis
        // olsa bile HENUZ gosterilmemis olmali.
        jasmine.clock().tick(1500);
        expect(service.showInactivityWarning()).toBeFalse();
    });
});

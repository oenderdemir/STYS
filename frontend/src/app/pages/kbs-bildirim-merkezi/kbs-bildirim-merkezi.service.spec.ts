import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { KbsBildirimMerkeziService } from './kbs-bildirim-merkezi.service';

describe('KbsBildirimMerkeziService', () => {
    let service: KbsBildirimMerkeziService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [KbsBildirimMerkeziService, provideHttpClient(), provideHttpClientTesting()] });
        service = TestBed.inject(KbsBildirimMerkeziService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('sonucu belirsiz bildirim icin denetlenebilir mutabakat istegi gonderir', () => {
        service.mutabakat(42, 'Islenmedi', 'Sentetik kontrol aciklamasi.', 'REF-42').subscribe();
        const request = http.expectOne(x => x.url.endsWith('/ui/kbs/bildirimler/42/mutabakat'));
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ karar: 'Islenmedi', aciklama: 'Sentetik kontrol aciklamasi.', kurumReferansNo: 'REF-42' });
        request.flush(null);
    });

    it('EGM dogrulamasinda kullanici kararini aciklamayla gonderir', () => {
        service.egmDogrula(84, true, 'Sentetik EGM kontrolu.', null).subscribe();
        const request = http.expectOne(x => x.url.endsWith('/ui/kbs/bildirimler/84/egm-dogrulama'));
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ basarili: true, aciklama: 'Sentetik EGM kontrolu.', kurumReferansNo: null });
        request.flush(null);
    });
});

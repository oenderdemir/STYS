import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EBelgeYonetimiService } from './e-belge-yonetimi.service';
import { EBelgeEntegrasyonYontemi, KurumEBelgeReadinessModel } from './e-belge-yonetimi.dto';

/**
 * Faz 2B.11 görev md.41 madde 1-3 - policy/readiness/revizyon DTO'larının GERÇEKTEN doğru
 * endpoint'lerden yüklendiğini (mevcut `GET policy`/`PUT policy`/`GET revizyonlar` sözleşmesi
 * DEĞİŞTİRİLMEDEN, YENİ `GET readiness` eklendi - görev md.47) kanıtlar.
 */
describe('EBelgeYonetimiService', () => {
    let service: EBelgeYonetimiService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule]
        });
        service = TestBed.inject(EBelgeYonetimiService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());

    it('getPolitika politika hic yapilandirilmamissa (success + data:null) hata FIRLATMAZ, null doner', (done) => {
        service.getPolitika(7).subscribe((result) => {
            expect(result).toBeNull();
            done();
        });

        const req = httpMock.expectOne((r) => r.url.endsWith('/ui/kurumlar/7/e-belge-politikasi') && r.method === 'GET');
        req.flush({ success: true, message: '', data: null, errors: [] });
    });

    it('getReadiness readiness endpointine istek atar ve DTO alanlarini oldugu gibi doner', (done) => {
        const readiness: KurumEBelgeReadinessModel = {
            kurumId: 7,
            politikaYapilandirilmisMi: true,
            politikaAktifMi: true,
            entegrasyonYontemi: EBelgeEntegrasyonYontemi.GibPortal,
            yontemOperasyonelMi: true,
            kurumAktivasyonYerelTarihi: '2020-01-01T00:00:00',
            globalMinimumAktivasyonYerelTarihi: '2026-09-15T00:00:00',
            globalProcessingAktifMi: true,
            globalProcessingDurumu: 'Active',
            yerelSnapshotGerekliMi: true,
            yerelUnsignedUblGerekliMi: true,
            ublFeatureUygulanabilirMi: true,
            ublFeatureAktifMi: true,
            yerelImzaGerekliMi: false,
            otomatikGonderimGerekliMi: false,
            saticiAnaVerileriHazirMi: true,
            eksikSaticiAlanlari: [],
            signingGateUygulanabilirMi: false,
            signingSuAnMumkunMu: false,
            islemeHazirMi: true,
            blokajNedenleri: [],
            yontemler: []
        };

        service.getReadiness(7).subscribe((result) => {
            expect(result).toEqual(readiness);
            done();
        });

        const req = httpMock.expectOne((r) => r.url.endsWith('/ui/kurumlar/7/e-belge-politikasi/readiness') && r.method === 'GET');
        req.flush({ success: true, message: '', data: readiness, errors: [] });
    });

    it('getRevizyonlar revizyonlar endpointine istek atar', (done) => {
        service.getRevizyonlar(7).subscribe((result) => {
            expect(result.length).toBe(1);
            done();
        });

        const req = httpMock.expectOne((r) => r.url.endsWith('/ui/kurumlar/7/e-belge-politikasi/revizyonlar') && r.method === 'GET');
        req.flush({
            success: true,
            message: '',
            data: [
                {
                    eskiSurum: 0,
                    yeniSurum: 1,
                    eskiYontem: EBelgeEntegrasyonYontemi.Yapilandirilmadi,
                    yeniYontem: EBelgeEntegrasyonYontemi.GibPortal,
                    eskiAktifMi: false,
                    yeniAktifMi: true,
                    degisiklikZamaniUtc: '2026-08-07T10:00:00Z',
                    degisiklikNedeni: 'test',
                    degistirenKullanici: 'admin'
                }
            ],
            errors: []
        });
    });

    it('updatePolitika PUT ile dogru payloadi GONDERIR', (done) => {
        service
            .updatePolitika(7, {
                entegrasyonYontemi: EBelgeEntegrasyonYontemi.GibPortal,
                aktifMi: true,
                aktivasyonYerelTarihi: '2026-09-15',
                hariciSistemKodu: null,
                degisiklikNedeni: 'test onayi',
                rowVersion: ''
            })
            .subscribe(() => done());

        const req = httpMock.expectOne((r) => r.url.endsWith('/ui/kurumlar/7/e-belge-politikasi') && r.method === 'PUT');
        expect(req.request.body.degisiklikNedeni).toBe('test onayi');
        req.flush({
            success: true,
            message: '',
            data: {
                kurumId: 7,
                entegrasyonYontemi: EBelgeEntegrasyonYontemi.GibPortal,
                aktifMi: true,
                aktivasyonYerelTarihi: '2026-09-15T00:00:00',
                hariciSistemKodu: null,
                politikaSurumu: 1,
                rowVersion: 'AAAA'
            },
            errors: []
        });
    });
});

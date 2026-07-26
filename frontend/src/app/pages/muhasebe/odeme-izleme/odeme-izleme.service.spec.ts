import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiResponse } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { OdemeIzlemeService } from './odeme-izleme.service';

describe('OdemeIzlemeService', () => {
    let service: OdemeIzlemeService;
    let httpMock: HttpTestingController;
    const baseUrl = `${getApiBaseUrl()}/ui/muhasebe/odeme-izleme`;

    beforeEach(() => {
        TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
        service = TestBed.inject(OdemeIzlemeService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => httpMock.verify());

    it('ara: sayfa/filtre parametrelerini dogru gonderir', () => {
        service.ara(2, 10, { tesisId: 5, belgeNo: 'ABC', durum: 'Aktif' }).subscribe();

        const req = httpMock.expectOne(
            (r) =>
                r.url === baseUrl &&
                r.params.get('pageNumber') === '2' &&
                r.params.get('pageSize') === '10' &&
                r.params.get('tesisId') === '5' &&
                r.params.get('belgeNo') === 'ABC' &&
                r.params.get('durum') === 'Aktif'
        );
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: { items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: true, hasNextPage: false }, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('getDetay: dogru URL kullanir ve basarisiz yanitta hata firlatir', (done) => {
        service.getDetay(42).subscribe({
            next: () => fail('basarili sonuc beklenmiyordu'),
            error: (err: Error) => {
                expect(err.message).toContain('bulunamadı');
                done();
            }
        });

        const req = httpMock.expectOne(`${baseUrl}/42`);
        expect(req.request.method).toBe('GET');
        req.flush({ success: false, message: 'Ödeme kaydı bulunamadı.', data: null, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('getCariHareketDokumu: cariKartId parametresini gonderir', () => {
        service.getCariHareketDokumu({ cariKartId: 7 }).subscribe();

        const req = httpMock.expectOne((r) => r.url === `${baseUrl}/cari-hareket-dokumu` && r.params.get('cariKartId') === '7');
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: {}, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('ara: yeni filtreler (fis no, rezervasyon, IBAN, donem) query string olarak gonderilir', () => {
        service.ara(1, 20, {
            tesisId: 1, muhasebeFisNo: 'FIS-9', rezervasyonReferansNo: 'REZ-5', iban: 'TR33',
            olusturanKullanici: 'ali', maliYil: 2026, donem: 7
        }).subscribe();

        const req = httpMock.expectOne(
            (r) =>
                r.url === baseUrl &&
                r.params.get('muhasebeFisNo') === 'FIS-9' &&
                r.params.get('rezervasyonReferansNo') === 'REZ-5' &&
                r.params.get('iban') === 'TR33' &&
                r.params.get('olusturanKullanici') === 'ali' &&
                r.params.get('maliYil') === '2026' &&
                r.params.get('donem') === '7'
        );
        req.flush({ success: true, message: '', data: { items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('caprazAra: dogru URL ve kopukluk/sayfa parametrelerini kullanir', () => {
        service.caprazAra(2, 10, { tesisId: 3, kopuklukTipi: 'ValorKaydiOlmayanPosTahsilati', sadeceKopukOlanlar: true }).subscribe();

        const req = httpMock.expectOne(
            (r) =>
                r.url === `${baseUrl}/capraz-arama` &&
                r.params.get('pageNumber') === '2' &&
                r.params.get('pageSize') === '10' &&
                r.params.get('tesisId') === '3' &&
                r.params.get('kopuklukTipi') === 'ValorKaydiOlmayanPosTahsilati' &&
                r.params.get('sadeceKopukOlanlar') === 'true'
        );
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: { items: [], pageNumber: 2, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: true, hasNextPage: false }, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('karsilastir: tarih/tutar/paraBirimi zorunlu parametrelerini gonderir', () => {
        service.karsilastir({ tesisId: 1, tarih: '2026-07-24', tutar: 450, paraBirimi: 'TRY', belgeNoTahmini: 'DEKONT1' }).subscribe();

        const req = httpMock.expectOne(
            (r) =>
                r.url === `${baseUrl}/karsilastir` &&
                r.params.get('tarih') === '2026-07-24' &&
                r.params.get('tutar') === '450' &&
                r.params.get('paraBirimi') === 'TRY' &&
                r.params.get('belgeNoTahmini') === 'DEKONT1'
        );
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: [], errors: [] } satisfies ApiResponse<unknown>);
    });
});

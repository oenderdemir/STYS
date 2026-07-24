import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiResponse } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { NakitBankaPozisyonuOzetModel } from './nakit-banka-pozisyonu.dto';
import { NakitBankaPozisyonuService } from './nakit-banka-pozisyonu.service';

describe('NakitBankaPozisyonuService', () => {
    let service: NakitBankaPozisyonuService;
    let httpMock: HttpTestingController;
    const baseUrl = `${getApiBaseUrl()}/ui/muhasebe/nakit-banka-pozisyonu`;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule]
        });
        service = TestBed.inject(NakitBankaPozisyonuService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('getOzet: filtre parametrelerini dogru sekilde query string olarak gonderir', () => {
        service
            .getOzet({
                tesisId: 42,
                raporTarihi: '2026-07-24',
                hesapTuru: 'Banka',
                paraBirimi: 'TRY',
                valorDurumu: 'ValorBekliyor'
            })
            .subscribe();

        const req = httpMock.expectOne(
            (r) =>
                r.url === `${baseUrl}/ozet` &&
                r.params.get('tesisId') === '42' &&
                r.params.get('raporTarihi') === '2026-07-24' &&
                r.params.get('hesapTuru') === 'Banka' &&
                r.params.get('paraBirimi') === 'TRY' &&
                r.params.get('valorDurumu') === 'ValorBekliyor'
        );
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: {}, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('getOzet: tesisId <= 0 iken parametre GONDERILMEZ (tum tesis kapsami anlamina gelir)', () => {
        service.getOzet({ tesisId: 0 }).subscribe();

        const req = httpMock.expectOne(`${baseUrl}/ozet`);
        expect(req.request.params.has('tesisId')).toBeFalse();
        req.flush({ success: true, message: '', data: {}, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('getOzet: basarili yaniti dogru sekilde ac (unwrap) eder', (done) => {
        const beklenen: NakitBankaPozisyonuOzetModel = {
            raporTarihi: '2026-07-24',
            toplamNakit: 1000,
            toplamBankaMuhasebeBakiyesi: 2000,
            valoruGecmisBekleyenNet: 0,
            bugunGelecekNet: 0,
            yarinGelecekNet: 0,
            takip2_7GunGelecekNet: 0,
            sonraki7GundenSonraNet: 0,
            toplamBekleyenNetPos: 300,
            tahminiToplamBankaPozisyonu: 2300,
            mutabakatBekleyenToplam: 0,
            mutabakatBekleyenAdet: 0,
            hataliToplam: 0,
            hataliAdet: 0,
            uyariSayisi: 0,
            paraBirimiOzetleri: []
        };

        service.getOzet({ tesisId: 1 }).subscribe((sonuc) => {
            expect(sonuc).toEqual(beklenen);
            done();
        });

        const req = httpMock.expectOne((r) => r.url === `${baseUrl}/ozet`);
        req.flush({ success: true, message: '', data: beklenen, errors: [] } satisfies ApiResponse<NakitBankaPozisyonuOzetModel>);
    });

    it('getOzet: basarisiz yanit alindiginda hata firlatir', (done) => {
        service.getOzet({ tesisId: 1 }).subscribe({
            next: () => fail('Basarili sonuc BEKLENMIYORDU.'),
            error: (err: Error) => {
                expect(err.message).toContain('yetkiniz');
                done();
            }
        });

        const req = httpMock.expectOne((r) => r.url === `${baseUrl}/ozet`);
        req.flush({ success: false, message: 'Bu tesis için yetkiniz bulunmuyor.', data: null, errors: [] } satisfies ApiResponse<unknown>);
    });

    it('getValorTakvimi: dogru URL ve (verilmisse) raporTarihi parametresini kullanir', () => {
        service.getValorTakvimi(7, '2026-07-24').subscribe();

        const req = httpMock.expectOne(
            (r) => r.url === `${baseUrl}/banka-hesaplari/7/valor-takvimi` && r.params.get('raporTarihi') === '2026-07-24'
        );
        expect(req.request.method).toBe('GET');
        req.flush({ success: true, message: '', data: {}, errors: [] } satisfies ApiResponse<unknown>);
    });
});

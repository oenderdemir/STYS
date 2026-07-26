import { TestBed } from '@angular/core/testing';
import { ChangeDetectorRef, signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { OdemeAramaSatiriModel } from './odeme-izleme.dto';
import { OdemeIzlemePage } from './odeme-izleme';
import { OdemeIzlemeService } from './odeme-izleme.service';

function ornekSatir(): OdemeAramaSatiriModel {
    return {
        id: 1,
        belgeNo: 'B-001',
        belgeTarihi: '2026-07-24',
        belgeTipi: 'Tahsilat',
        durum: 'Aktif',
        tutar: 500,
        paraBirimi: 'TRY',
        odemeYontemi: 'Nakit',
        cariKartId: 10,
        cariKodu: 'C-01',
        cariUnvan: 'Test Müşteri',
        uyariSayisi: 1
    };
}

describe('OdemeIzlemePage', () => {
    let serviceSpy: jasmine.SpyObj<OdemeIzlemeService>;
    let tesisContextStub: Partial<MuhasebeTesisContextService> & { seciliTesis: ReturnType<typeof signal> };

    function createComponent(): OdemeIzlemePage {
        TestBed.configureTestingModule({
            providers: [
                { provide: OdemeIzlemeService, useValue: serviceSpy },
                { provide: MuhasebeTesisContextService, useValue: tesisContextStub },
                MessageService
            ]
        });
        const fixture = TestBed.createComponent(OdemeIzlemePage);
        // ChangeDetectorRef.detectChanges gercek sablonu (p-table dahil) render eder - bu birim
        // testlerinde DOM/PrimeNG render zincirine ihtiyac YOK, yalnizca component STATE'i test
        // ediliyor; gercek render zinciri, fixture hicbir zaman fixture.detectChanges() ile duzgun
        // baslatilmadigi icin PrimeNG p-table'in [lazy] onLazyLoad'unu tekrar tekrar tetiklemesine
        // (sonsuz dongu) yol aciyordu - bu yuzden bilincli olarak no-op yapilir.
        spyOn(fixture.componentRef.injector.get(ChangeDetectorRef), 'detectChanges');
        return fixture.componentInstance;
    }

    beforeEach(() => {
        serviceSpy = jasmine.createSpyObj<OdemeIzlemeService>('OdemeIzlemeService', ['ara', 'getDetay', 'getCariHareketDokumu', 'karsilastir']);
        tesisContextStub = {
            seciliTesis: signal({ id: 1, ad: 'Test Tesis' }),
            tesisler: signal([{ id: 1, ad: 'Test Tesis' }]),
            tesislerLoading: signal(false),
            tesislerError: signal(null),
            tesisSecenekleri: signal([{ label: 'Test Tesis', value: 1 }]),
            initialize: () => of([{ id: 1, ad: 'Test Tesis' }]),
            selectTesis: () => undefined,
            clearTesis: () => undefined,
            clearPersistedTesis: () => undefined,
            requireSeciliTesis: () => ({ id: 1, ad: 'Test Tesis' }),
            requireSeciliTesisId: () => 1
        } as unknown as Partial<MuhasebeTesisContextService> & { seciliTesis: ReturnType<typeof signal> };
    });

    afterEach(() => {
        TestBed.resetTestingModule();
    });

    it('ngOnInit: arama basariyla yuklenince state dogru set edilir', () => {
        serviceSpy.ara.and.returnValue(of({ items: [ornekSatir()], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1, hasPreviousPage: false, hasNextPage: false }));

        const component = createComponent();
        component.ngOnInit();

        expect(component.loading).toBeFalse();
        expect(component.satirlar.length).toBe(1);
        expect(component.toplamKayit).toBe(1);
        expect(component.errorMessage).toBeNull();
    });

    it('hata durumu: backend hatasi anlasilir mesajla bildirilir', () => {
        serviceSpy.ara.and.returnValue(throwError(() => new Error('Bu tesis için yetkiniz bulunmuyor.')));

        const component = createComponent();
        component.ngOnInit();

        expect(component.errorMessage).toContain('yetkiniz');
        expect(component.satirlar.length).toBe(0);
    });

    it('onFiltreDegisti: sayfayi 1e resetleyip yeniden yukler', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        const component = createComponent();
        component.ngOnInit();
        component.sayfa = 3;

        component.onFiltreDegisti();

        expect(component.sayfa).toBe(1);
        expect(serviceSpy.ara).toHaveBeenCalledTimes(2);
    });

    it('onSayfaDegisti: lazy-load event sayfa numarasini dogru hesaplar', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        const component = createComponent();
        component.ngOnInit();

        component.onSayfaDegisti({ first: 40, rows: 20 });

        expect(component.sayfa).toBe(3);
        expect(component.sayfaBoyutu).toBe(20);
    });

    it('openDetay: detay dialogunu acar ve verileri yukler', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        serviceSpy.getDetay.and.returnValue(
            of({
                id: 1, belgeNo: 'B-001', belgeTarihi: '2026-07-24', belgeTipi: 'Tahsilat', durum: 'Aktif', tutar: 500, paraBirimi: 'TRY',
                odemeYontemi: 'Nakit', cariKartId: 10, cariKodu: 'C-01', cariUnvan: 'Test Müşteri', kapatildiMi: false,
                bakiyeyeDahilMi: true, bakiyeyeDahilEdilmeDurumu: 'TamamenDahil',
                bakiyeyeDahilEdilmemeNedenKodlari: [], bakiyeyeDahilEdilmemeAciklamalari: [], uyarilar: []
            })
        );

        const component = createComponent();
        component.ngOnInit();
        component.openDetay(ornekSatir());

        expect(component.detayVisible).toBeTrue();
        expect(component.detay?.belgeNo).toBe('B-001');
    });

    it('bakiyeye dahil edilmeme gerekcelerinin gosterilmesi: neden kodlari ve aciklamalar state uzerinden okunur', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        serviceSpy.getDetay.and.returnValue(
            of({
                id: 2, belgeNo: 'B-002', belgeTarihi: '2026-07-24', belgeTipi: 'Tahsilat', durum: 'Aktif', tutar: 500, paraBirimi: 'TRY',
                odemeYontemi: 'Nakit', cariKartId: 10, cariKodu: 'C-01', cariUnvan: 'Test Müşteri', kapatildiMi: false,
                bakiyeyeDahilMi: false,
                bakiyeyeDahilEdilmeDurumu: 'DahilDegil',
                bakiyeyeDahilEdilmemeNedenKodlari: ['CariHareketiYok', 'ZorunluMuhasebeFisiYok'],
                bakiyeyeDahilEdilmemeAciklamalari: ['Cari hareket oluşmamış.', 'Muhasebe fişi üretilmemiş.'],
                uyarilar: []
            })
        );

        const component = createComponent();
        component.ngOnInit();
        component.openDetay(ornekSatir());

        // Aktif belge OLMASINA RAGMEN otomatik "dahil" sayilmaz.
        expect(component.detay?.bakiyeyeDahilMi).toBeFalse();
        expect(component.detay?.bakiyeyeDahilEdilmeDurumu).toBe('DahilDegil');
        expect(component.detay?.bakiyeyeDahilEdilmemeNedenKodlari.length).toBe(2);
    });

    it('getGuvenSeviyesiSeverity: her guven seviyesi beklenen renge eslenir', () => {
        const component = createComponent();
        expect(component.getGuvenSeviyesiSeverity('Kesin')).toBe('danger');
        expect(component.getGuvenSeviyesiSeverity('YuksekOlasilik')).toBe('warn');
        expect(component.getGuvenSeviyesiSeverity('IncelenmesiGereken')).toBe('info');
        expect(component.getGuvenSeviyesiSeverity('EslesmeYok')).toBe('secondary');
    });

    it('calistirKarsilastirma: sonuclari state uzerinden okunabilir sekilde set eder', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        serviceSpy.karsilastir.and.returnValue(
            of([{
                odemeId: 1, belgeNo: 'B-1', belgeTarihi: '2026-07-24', tutar: 100, paraBirimi: 'TRY', odemeYontemi: 'Nakit',
                cariUnvan: 'X', guvenSeviyesi: 'Kesin', gerekce: 'test',
                eslesenAlanlar: ['Tutar', 'Belge/dekont no'], uyusmayanAlanlar: [], tarihBirebirMi: true, tarihFarkiGun: 0
            }])
        );

        const component = createComponent();
        component.ngOnInit();
        component.beyanTutar = 100;
        component.calistirKarsilastirma();

        expect(component.karsilastirSonuclari.length).toBe(1);
        expect(component.karsilastirSonuclari[0].guvenSeviyesi).toBe('Kesin');
    });

    it('kismi belge no eslesmesi KESIN olarak gosterilmez ve toleransli tarih birebir sayilmaz', () => {
        serviceSpy.ara.and.returnValue(of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false }));
        serviceSpy.karsilastir.and.returnValue(
            of([{
                odemeId: 2, belgeNo: 'B-2', belgeTarihi: '2026-07-23', tutar: 100, paraBirimi: 'TRY', odemeYontemi: 'Nakit',
                cariUnvan: 'Y', guvenSeviyesi: 'IncelenmesiGereken',
                gerekce: 'Yalnızca tutar, para birimi ve tarih uyuşuyor.',
                eslesenAlanlar: ['Tutar', 'Para birimi'],
                uyusmayanAlanlar: ['Belge/dekont no'],
                tarihBirebirMi: false, tarihFarkiGun: 1
            }])
        );

        const component = createComponent();
        component.ngOnInit();
        component.beyanTutar = 100;
        component.calistirKarsilastirma();

        const s = component.karsilastirSonuclari[0];
        expect(s.guvenSeviyesi).not.toBe('Kesin');
        expect(s.tarihBirebirMi).toBeFalse();
        expect(s.tarihFarkiGun).toBe(1);
        expect(component.getGuvenSeviyesiSeverity(s.guvenSeviyesi)).toBe('info');
    });

    it('para bicimlendirme: pozitif/negatif/sifir tutarlar farkli siniflar alir', () => {
        const component = createComponent();
        expect(component.getTutarClass(10)).toBe('text-green-600');
        expect(component.getTutarClass(-10)).toBe('text-red-500');
        expect(component.getTutarClass(0)).toBe('');
    });
});

import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { RezervasyonUzatmaSecenegiDto, RezervasyonUzatmaSecenekleriDto, RezervasyonUzatmaSonucDto } from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';
import { RezervasyonUzatmaDialogComponent } from './rezervasyon-uzatma-dialog';

function ornekSecenek(overrides?: Partial<RezervasyonUzatmaSecenegiDto>): RezervasyonUzatmaSecenegiDto {
    return {
        senaryoKodu: 'UZT-ABC123',
        senaryoTipi: 'AyniOdadaDevam',
        aciklama: 'Mevcut odada uzatma',
        odaDegisimSayisi: 0,
        ekBazUcret: 500,
        ekNihaiUcret: 600,
        paraBirimi: 'TRY',
        fiyatlamaTipi: 'Standart',
        fiyatUyarisi: null,
        segmentler: [
            {
                baslangicTarihi: '2026-03-10T10:00:00',
                bitisTarihi: '2026-03-11T10:00:00',
                odaAtamalari: [
                    { odaId: 1, odaNo: '101', binaId: 1, binaAdi: 'A Blok', odaTipiId: 1, odaTipiAdi: 'Standart', paylasimliMi: false, kapasite: 1, ayrilanKisiSayisi: 1 }
                ]
            }
        ],
        ...overrides
    };
}

function ornekSecenekler(overrides?: Partial<RezervasyonUzatmaSecenekleriDto>): RezervasyonUzatmaSecenekleriDto {
    return {
        rezervasyonId: 1,
        referansNo: 'REZ-1',
        mevcutCikisTarihi: '2026-03-10T10:00:00',
        yeniCikisTarihi: '2026-03-11T10:00:00',
        sonucKodu: 'SecenekBulundu',
        mesaj: '1 adet uzatma secenegi bulundu.',
        secenekler: [ornekSecenek()],
        ...overrides
    };
}

function ornekSonuc(overrides?: Partial<RezervasyonUzatmaSonucDto>): RezervasyonUzatmaSonucDto {
    return {
        rezervasyonId: 1,
        referansNo: 'REZ-1',
        senaryoKodu: 'UZT-ABC123',
        senaryoTipi: 'AyniOdadaDevam',
        eskiCikisTarihi: '2026-03-10T10:00:00',
        yeniCikisTarihi: '2026-03-11T10:00:00',
        ekBazUcret: 500,
        ekNihaiUcret: 600,
        yeniToplamBazUcret: 1500,
        yeniToplamUcret: 1600,
        paraBirimi: 'TRY',
        segmentler: [],
        mesaj: 'Rezervasyon uzatma islemi basariyla kaydedildi.',
        ...overrides
    };
}

describe('RezervasyonUzatmaDialogComponent', () => {
    let serviceSpy: jasmine.SpyObj<RezervasyonYonetimiService>;

    function createComponent(): RezervasyonUzatmaDialogComponent {
        TestBed.configureTestingModule({
            providers: [
                { provide: RezervasyonYonetimiService, useValue: serviceSpy },
                MessageService
            ]
        });

        return TestBed.createComponent(RezervasyonUzatmaDialogComponent).componentInstance;
    }

    beforeEach(() => {
        serviceSpy = jasmine.createSpyObj<RezervasyonYonetimiService>('RezervasyonYonetimiService', ['getUzatmaSecenekleri', 'uzatRezervasyon']);
    });

    it('gecersiz yeni cikis tarihinde (mevcut cikis tarihinden once/esit) secenek istegi yapilmaz', () => {
        const component = createComponent();
        component.rezervasyonId = 1;
        component.mevcutCikisTarihi = '2026-03-10T10:00:00';

        // Mevcut cikis tarihiyle AYNI tarih - gecersiz.
        component.yeniCikisTarihi = new Date(2026, 2, 10, 10, 0, 0);
        expect(component.isYeniCikisTarihiGecerli()).toBeFalse();
        expect(component.canGetirSecenekleri()).toBeFalse();

        component.getirSecenekleri();

        expect(serviceSpy.getUzatmaSecenekleri).not.toHaveBeenCalled();
    });

    it('gecerli yeni cikis tarihinde secenek istegi yapilir', () => {
        serviceSpy.getUzatmaSecenekleri.and.returnValue(of(ornekSecenekler()));

        const component = createComponent();
        component.rezervasyonId = 1;
        component.mevcutCikisTarihi = '2026-03-10T10:00:00';
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);

        expect(component.canGetirSecenekleri()).toBeTrue();
        component.getirSecenekleri();

        expect(serviceSpy.getUzatmaSecenekleri).toHaveBeenCalledTimes(1);
        expect(component.secenekler?.secenekler.length).toBe(1);
    });

    it('MusaitlikYok sonucunda backend mesaji anlasilir bir uyari olarak state uzerinden okunabilir', () => {
        serviceSpy.getUzatmaSecenekleri.and.returnValue(
            of(ornekSecenekler({ sonucKodu: 'MusaitlikYok', mesaj: 'Secilen tarih araliginda uygun oda bulunamadi.', secenekler: [] }))
        );

        const component = createComponent();
        component.rezervasyonId = 1;
        component.mevcutCikisTarihi = '2026-03-10T10:00:00';
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);
        component.getirSecenekleri();

        expect(component.sonucMusaitlikYokMu).toBeTrue();
        expect(component.secenekler?.mesaj).toBe('Secilen tarih araliginda uygun oda bulunamadi.');
    });

    it('secenek secilmeden kaydetme yapilamaz (canUzat false, kaydetme tetiklenmez)', () => {
        const component = createComponent();
        component.rezervasyonId = 1;
        component.seciliSecenek = null;

        expect(component.canUzat()).toBeFalse();

        component.rezervasyonuUzatOnayla();

        expect(serviceSpy.uzatRezervasyon).not.toHaveBeenCalled();
    });

    it('kaydetme isteginde YALNIZCA yeniCikisTarihi ve senaryoKodu gonderilir - fiyat/segment bilgisi gonderilmez', () => {
        serviceSpy.uzatRezervasyon.and.returnValue(of(ornekSonuc()));

        const component = createComponent();
        component.rezervasyonId = 1;
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);
        const secenek = ornekSecenek();

        (component as unknown as { executeUzat: (s: RezervasyonUzatmaSecenegiDto) => void }).executeUzat(secenek);

        expect(serviceSpy.uzatRezervasyon).toHaveBeenCalledTimes(1);
        const [, request] = serviceSpy.uzatRezervasyon.calls.mostRecent().args;
        expect(Object.keys(request).sort()).toEqual(['senaryoKodu', 'yeniCikisTarihi'].sort());
        expect(request.senaryoKodu).toBe(secenek.senaryoKodu);
    });

    it('409 sonrasinda eski secim temizlenir ve secenekler ayni tarihle yeniden getirilir', () => {
        serviceSpy.uzatRezervasyon.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { message: 'Plan artik gecerli degil.' } })));
        serviceSpy.getUzatmaSecenekleri.and.returnValue(of(ornekSecenekler()));

        const component = createComponent();
        component.rezervasyonId = 1;
        component.mevcutCikisTarihi = '2026-03-10T10:00:00';
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);
        component.seciliSecenek = ornekSecenek();

        (component as unknown as { executeUzat: (s: RezervasyonUzatmaSecenegiDto) => void }).executeUzat(component.seciliSecenek);

        expect(component.seciliSecenek).toBeNull();
        // 409 sonrasi baska bir secenek OTOMATIK kaydedilmez - yalnizca secenekler yeniden getirilir.
        expect(serviceSpy.uzatRezervasyon).toHaveBeenCalledTimes(1);
        expect(serviceSpy.getUzatmaSecenekleri).toHaveBeenCalledTimes(1);
    });

    it('yukleme sirasinda yinelenen secenek istegi engellenir', () => {
        const subject = new Subject<RezervasyonUzatmaSecenekleriDto>();
        serviceSpy.getUzatmaSecenekleri.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.rezervasyonId = 1;
        component.mevcutCikisTarihi = '2026-03-10T10:00:00';
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);

        component.getirSecenekleri();
        expect(component.loading).toBeTrue();

        // Istek devam ederken tekrar tetiklensin - ikinci cagri engellenmeli.
        component.getirSecenekleri();

        expect(serviceSpy.getUzatmaSecenekleri).toHaveBeenCalledTimes(1);

        subject.next(ornekSecenekler());
        subject.complete();
    });

    it('kaydetme sirasinda yinelenen istek engellenir', () => {
        const subject = new Subject<RezervasyonUzatmaSonucDto>();
        serviceSpy.uzatRezervasyon.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.rezervasyonId = 1;
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);
        const secenek = ornekSecenek();

        const instance = component as unknown as { executeUzat: (s: RezervasyonUzatmaSecenegiDto) => void };
        instance.executeUzat(secenek);
        expect(component.saving).toBeTrue();

        // Kaydetme devam ederken tekrar tetiklensin - ikinci cagri engellenmeli.
        instance.executeUzat(secenek);

        expect(serviceSpy.uzatRezervasyon).toHaveBeenCalledTimes(1);

        subject.next(ornekSonuc());
        subject.complete();
    });

    it('dialog kapatildiginda (visible=false) state sifirlanir', () => {
        const component = createComponent();
        component.rezervasyonId = 1;
        component.seciliSecenek = ornekSecenek();
        component.secenekler = ornekSecenekler();
        component.yeniCikisTarihi = new Date(2026, 2, 11, 10, 0, 0);

        component.visible = false;
        component.ngOnChanges({
            visible: {
                previousValue: true,
                currentValue: false,
                firstChange: false,
                isFirstChange: () => false
            }
        });

        expect(component.seciliSecenek).toBeNull();
        expect(component.secenekler).toBeNull();
        expect(component.yeniCikisTarihi).toBeNull();
    });

    it('yeni cikis tarihi secildiginde saat kullanicidan alinmaz - tesisin varsayilan cikis saatine sabitlenir', () => {
        const component = createComponent();
        component.tesisCikisSaati = '10:00:00';

        // Kullanici yalnizca TARIH secer (p-datepicker artik [showTime] icermez) - datepicker'in
        // urettigi Date nesnesinin saat kismi ne olursa olsun (varsayilan gece yarisi dahil),
        // onYeniCikisTarihiChange bunu tesisin cikis saatiyle DEGISTIRMELIDIR.
        component.onYeniCikisTarihiChange(new Date(2026, 2, 15, 0, 0, 0));

        expect(component.yeniCikisTarihi?.getFullYear()).toBe(2026);
        expect(component.yeniCikisTarihi?.getMonth()).toBe(2);
        expect(component.yeniCikisTarihi?.getDate()).toBe(15);
        expect(component.yeniCikisTarihi?.getHours()).toBe(10);
        expect(component.yeniCikisTarihi?.getMinutes()).toBe(0);
    });
});

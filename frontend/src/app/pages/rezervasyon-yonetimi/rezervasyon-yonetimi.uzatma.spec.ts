import { ActivatedRoute } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService } from '../auth';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';
import { RezervasyonDetayDto, RezervasyonListeDto } from './rezervasyon-yonetimi.dto';
import { RezervasyonYonetimi } from './rezervasyon-yonetimi';
import { RezervasyonYonetimiService } from './rezervasyon-yonetimi.service';

function ornekKayit(overrides?: Partial<RezervasyonListeDto>): RezervasyonListeDto {
    return {
        id: 1,
        referansNo: 'REZ-1',
        kaynak: 'Manuel',
        tesisId: 1,
        misafirAdiSoyadi: 'Test Misafir',
        misafirTelefon: '000',
        misafirEposta: null,
        tcKimlikNo: null,
        pasaportNo: null,
        misafirCinsiyeti: null,
        kisiSayisi: 1,
        girisTarihi: '2026-03-08T14:00:00',
        cikisTarihi: '2026-03-10T10:00:00',
        toplamUcret: 1000,
        odenenTutar: 0,
        kalanTutar: 1000,
        paraBirimi: 'TRY',
        rezervasyonDurumu: 'CheckInTamamlandi',
        fiyatlamaOzeti: '',
        konaklayanPlaniTamamlandi: true,
        gelenKonaklayanSayisi: 1,
        bekleyenKonaklayanSayisi: 0,
        ayrilanKonaklayanSayisi: 1,
        odaDegisimiGerekli: false,
        ...overrides
    };
}

describe('RezervasyonYonetimi - uzatma entegrasyonu', () => {
    let authServiceStub: { hasPermission: jasmine.Spy };
    let serviceSpy: jasmine.SpyObj<RezervasyonYonetimiService>;

    function createComponent(): RezervasyonYonetimi {
        TestBed.configureTestingModule({
            providers: [
                { provide: RezervasyonYonetimiService, useValue: serviceSpy },
                { provide: TesisYonetimiService, useValue: {} },
                { provide: AuthService, useValue: authServiceStub },
                { provide: ActivatedRoute, useValue: { snapshot: { queryParams: {} } } }
            ]
        });

        return TestBed.createComponent(RezervasyonYonetimi).componentInstance;
    }

    beforeEach(() => {
        authServiceStub = { hasPermission: jasmine.createSpy('hasPermission').and.returnValue(true) };
        serviceSpy = jasmine.createSpyObj<RezervasyonYonetimiService>('RezervasyonYonetimiService', ['searchRezervasyonlar', 'getRezervasyonDetay']);
    });

    it('Manage yetkisi VE CheckInTamamlandi durumunda Uzat aksiyonu acilabilir/gorunur', () => {
        const component = createComponent();
        const kayit = ornekKayit({ rezervasyonDurumu: 'CheckInTamamlandi' });

        expect(component.canOpenUzatmaDialog(kayit)).toBeTrue();
        expect(component.getUzatmaDisabledReason(kayit)).toBeNull();

        const rowActions = component.getRowActions(kayit);
        const uzatAction = rowActions.find((x) => x.label === 'Uzat');
        expect(uzatAction).withContext('Uzat aksiyonu row menusunde bulunmali').toBeTruthy();
        expect(uzatAction?.disabled).toBeFalsy();
    });

    it('Manage yetkisi YOKSA Uzat aksiyonu gorunmez', () => {
        authServiceStub.hasPermission.and.returnValue(false);
        const component = createComponent();
        const kayit = ornekKayit({ rezervasyonDurumu: 'CheckInTamamlandi' });

        expect(component.canOpenUzatmaDialog(kayit)).toBeFalse();
        expect(component.getUzatmaDisabledReason(kayit)).toBe('Yonetim yetkisi yok');

        const rowActions = component.getRowActions(kayit);
        expect(rowActions.find((x) => x.label === 'Uzat')).toBeFalsy();
    });

    it('CheckInTamamlandi DISINDAKI bir durumda Uzat aksiyonu devre disi kalir', () => {
        const component = createComponent();
        const kayit = ornekKayit({ rezervasyonDurumu: 'Onayli' });

        expect(component.canOpenUzatmaDialog(kayit)).toBeFalse();
        expect(component.getUzatmaDisabledReason(kayit)).toBe('Check-in tamamlanmadi');

        const rowActions = component.getRowActions(kayit);
        const uzatAction = rowActions.find((x) => x.label?.toString().startsWith('Uzat'));
        expect(uzatAction?.disabled).toBeTrue();

        component.openUzatmaDialog(kayit);
        expect(component.uzatmaDialogVisible).toBeFalse();
    });

    it('openUzatmaDialog: izinliyken diyalog durumunu kayittan doldurur', () => {
        const component = createComponent();
        const kayit = ornekKayit({ id: 42, referansNo: 'REZ-42', cikisTarihi: '2026-04-01T10:00:00' });

        component.openUzatmaDialog(kayit);

        expect(component.uzatmaDialogVisible).toBeTrue();
        expect(component.uzatmaRezervasyonId).toBe(42);
        expect(component.uzatmaReferansNo).toBe('REZ-42');
        expect(component.uzatmaMevcutCikisTarihi).toBe('2026-04-01T10:00:00');
    });

    it('onUzatmaSaved: rezervasyon listesini yeniler ve acik detay onbellegini temizleyip yeniden yukler', () => {
        serviceSpy.searchRezervasyonlar.and.returnValue(of({ kayitlar: [], toplamKayitSayisi: 0, page: 1, pageSize: 20 }));
        serviceSpy.getRezervasyonDetay.and.returnValue(of({} as unknown as RezervasyonDetayDto));

        const component = createComponent();
        component.uzatmaRezervasyonId = 7;
        component.rezervasyonDetayById[7] = { referansNo: 'ESKI-ONBELLEK' } as unknown as RezervasyonDetayDto;

        component.onUzatmaSaved();

        // Liste yenilenir (searchRezervasyonlar cagrilir) VE acik detay onbellegi temizlenip
        // (eski deger artik yok) yeniden yuklenir (getRezervasyonDetay TEKRAR cagrilir).
        expect(serviceSpy.searchRezervasyonlar).toHaveBeenCalledTimes(1);
        expect(serviceSpy.getRezervasyonDetay).toHaveBeenCalledWith(7);
        expect(component.rezervasyonDetayById[7]).not.toEqual({ referansNo: 'ESKI-ONBELLEK' } as unknown as RezervasyonDetayDto);
    });
});

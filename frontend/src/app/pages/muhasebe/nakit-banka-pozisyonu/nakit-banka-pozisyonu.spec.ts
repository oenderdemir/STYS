import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { BankaHesapPozisyonuModel, NakitBankaPozisyonuOzetModel } from './nakit-banka-pozisyonu.dto';
import { NakitBankaPozisyonuPage, bugunIstanbul } from './nakit-banka-pozisyonu';
import { NakitBankaPozisyonuService } from './nakit-banka-pozisyonu.service';

function ornekOzet(): NakitBankaPozisyonuOzetModel {
    return {
        raporTarihi: '2026-07-24',
        toplamNakit: 1500,
        toplamBankaMuhasebeBakiyesi: -250,
        valoruGecmisBekleyenNet: 0,
        bugunGelecekNet: 0,
        yarinGelecekNet: 0,
        takip2_7GunGelecekNet: 0,
        sonraki7GundenSonraNet: 0,
        toplamBekleyenNetPos: 100,
        tahminiToplamBankaPozisyonu: -150,
        mutabakatBekleyenToplam: 0,
        mutabakatBekleyenAdet: 0,
        hataliToplam: 0,
        hataliAdet: 0,
        uyariSayisi: 0,
        paraBirimiOzetleri: []
    };
}

function ornekBankaHesabi(): BankaHesapPozisyonuModel {
    return {
        kasaBankaHesapId: 5,
        bankaAdi: 'Test Banka',
        hesapAdi: 'Test Hesap',
        iban: 'TR330006100519786457841326',
        paraBirimi: 'TRY',
        stysMuhasebeBakiyesi: 1000,
        valoruGecmisBekleyenNet: 0,
        bugunGelecekNet: 0,
        yarinGelecekNet: 0,
        takip2_7GunGelecekNet: 0,
        sonraki7GundenSonraNet: 0,
        toplamBekleyenNet: 0,
        tahminiBakiye: 1000,
        mutabakatBekleyenNet: 0,
        mutabakatBekleyenAdet: 0,
        hataliNet: 0,
        hataliAdet: 0
    };
}

describe('NakitBankaPozisyonuPage', () => {
    let serviceSpy: jasmine.SpyObj<NakitBankaPozisyonuService>;
    let tesisContextStub: Partial<MuhasebeTesisContextService> & { seciliTesis: ReturnType<typeof signal> };

    function createComponent(): NakitBankaPozisyonuPage {
        TestBed.configureTestingModule({
            providers: [
                { provide: NakitBankaPozisyonuService, useValue: serviceSpy },
                { provide: MuhasebeTesisContextService, useValue: tesisContextStub },
                MessageService
            ]
        });

        return TestBed.createComponent(NakitBankaPozisyonuPage).componentInstance;
    }

    beforeEach(() => {
        serviceSpy = jasmine.createSpyObj<NakitBankaPozisyonuService>('NakitBankaPozisyonuService', ['getOzet', 'getHesaplar', 'getValorTakvimi']);
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

    it('bugunIstanbul: gecerli bir Date nesnesi dondurur (bugunun tarihiyle tutarli)', () => {
        const sonuc = bugunIstanbul();
        expect(sonuc instanceof Date).toBeTrue();
        expect(isNaN(sonuc.getTime())).toBeFalse();
    });

    it('ngOnInit: ozet ve hesaplar basariyla yuklenince state dogru sekilde set edilir', () => {
        serviceSpy.getOzet.and.returnValue(of(ornekOzet()));
        serviceSpy.getHesaplar.and.returnValue(of({ raporTarihi: '2026-07-24', kasaHesaplari: [], bankaHesaplari: [ornekBankaHesabi()], uyarilar: [] }));

        const component = createComponent();
        component.ngOnInit();

        expect(component.loading).toBeFalse();
        expect(component.ozet?.toplamNakit).toBe(1500);
        expect(component.hesaplar?.bankaHesaplari.length).toBe(1);
        expect(component.errorMessage).toBeNull();
    });

    it('bos sonuc: kasa ve banka listeleri bos donerse hata olusturmadan bos state gosterir', () => {
        serviceSpy.getOzet.and.returnValue(of(ornekOzet()));
        serviceSpy.getHesaplar.and.returnValue(of({ raporTarihi: '2026-07-24', kasaHesaplari: [], bankaHesaplari: [], uyarilar: [] }));

        const component = createComponent();
        component.ngOnInit();

        expect(component.hesaplar?.kasaHesaplari.length).toBe(0);
        expect(component.hesaplar?.bankaHesaplari.length).toBe(0);
        expect(component.errorMessage).toBeNull();
    });

    it('hata durumu: backend hatasi kullaniciya anlasilir bir mesajla bildirilir', () => {
        serviceSpy.getOzet.and.returnValue(throwError(() => new Error('Bu tesis için yetkiniz bulunmuyor.')));
        serviceSpy.getHesaplar.and.returnValue(of({ raporTarihi: '2026-07-24', kasaHesaplari: [], bankaHesaplari: [], uyarilar: [] }));

        const component = createComponent();
        component.ngOnInit();

        expect(component.errorMessage).toContain('yetkiniz');
        expect(component.ozet).toBeNull();
    });

    it('para bicimlendirme/gorsel ayrim: pozitif ve negatif tutarlar farkli CSS siniflari alir', () => {
        const component = createComponent();
        expect(component.getTutarClass(100)).toBe('text-green-600');
        expect(component.getTutarClass(-100)).toBe('text-red-500');
        expect(component.getTutarClass(0)).toBe('');
    });

    it('IBAN formatlama: 4li gruplar halinde bosluklu gosterir ve bos/null degerde tire doner', () => {
        const component = createComponent();
        expect(component.formatIban('TR330006100519786457841326')).toBe('TR33 0006 1005 1978 6457 8413 26');
        expect(component.formatIban(null)).toBe('-');
        expect(component.formatIban(undefined)).toBe('-');
    });

    it('valor durumlarinin dogru etiketlenmesi: her durum beklenen severity ile eslenir', () => {
        const component = createComponent();
        expect(component.getDurumSeverity('ValorBekliyor')).toBe('info');
        expect(component.getDurumSeverity('MutabakatBekliyor')).toBe('warn');
        expect(component.getDurumSeverity('Hata')).toBe('danger');
        expect(component.getDurumSeverity('Aktarildi')).toBe('secondary');
    });

    it('satir detayinin acilmasi: openValorTakvimi valor takvimini yukler ve gun acma/kapama calisir', () => {
        serviceSpy.getOzet.and.returnValue(of(ornekOzet()));
        serviceSpy.getHesaplar.and.returnValue(of({ raporTarihi: '2026-07-24', kasaHesaplari: [], bankaHesaplari: [], uyarilar: [] }));
        serviceSpy.getValorTakvimi.and.returnValue(
            of({
                kasaBankaHesapId: 5,
                raporTarihi: '2026-07-24',
                gunler: [{ valorTarihi: '2026-07-24', islemSayisi: 2, brutTutar: 200, komisyonTutari: 4, netTutar: 196, detaylar: [] }]
            })
        );

        const component = createComponent();
        component.openValorTakvimi(ornekBankaHesabi());

        expect(component.takvimVisible).toBeTrue();
        expect(component.takvimData?.gunler.length).toBe(1);
        expect(component.isGunAcik('2026-07-24')).toBeFalse();

        component.toggleGun('2026-07-24');
        expect(component.isGunAcik('2026-07-24')).toBeTrue();

        component.toggleGun('2026-07-24');
        expect(component.isGunAcik('2026-07-24')).toBeFalse();
    });
});

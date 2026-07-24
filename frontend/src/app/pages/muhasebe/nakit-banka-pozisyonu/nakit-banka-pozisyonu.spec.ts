import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { BankaHesapPozisyonuModel, NakitBankaPozisyonuModel } from './nakit-banka-pozisyonu.dto';
import { NakitBankaPozisyonuPage, bugunIstanbul } from './nakit-banka-pozisyonu';
import { NakitBankaPozisyonuService } from './nakit-banka-pozisyonu.service';

function ornekSonuc(overrides?: Partial<NakitBankaPozisyonuModel>): NakitBankaPozisyonuModel {
    return {
        raporTarihi: '2026-07-24',
        gecmisTarihRaporuMu: false,
        ozet: {
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
            gecmisTarihRaporuMu: false,
            uyariSayisi: 0,
            paraBirimiOzetleri: []
        },
        kasaHesaplari: [],
        bankaHesaplari: [],
        uyarilar: [],
        uygulananFiltre: {},
        ...overrides
    };
}

function ornekBankaHesabi(): BankaHesapPozisyonuModel {
    return {
        kasaBankaHesapId: 5,
        tesisId: 1,
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
        serviceSpy = jasmine.createSpyObj<NakitBankaPozisyonuService>('NakitBankaPozisyonuService', ['getPozisyon', 'getValorTakvimi', 'getValorGunDetaylari']);
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

    it('ngOnInit: pozisyon TEK cagriyla basariyla yuklenince state dogru sekilde set edilir', () => {
        serviceSpy.getPozisyon.and.returnValue(of(ornekSonuc({ bankaHesaplari: [ornekBankaHesabi()] })));

        const component = createComponent();
        component.ngOnInit();

        expect(serviceSpy.getPozisyon).toHaveBeenCalledTimes(1);
        expect(component.loading).toBeFalse();
        expect(component.sonuc?.ozet.toplamNakit).toBe(1500);
        expect(component.sonuc?.bankaHesaplari.length).toBe(1);
        expect(component.errorMessage).toBeNull();
    });

    it('bos sonuc: kasa ve banka listeleri bos donerse hata olusturmadan bos state gosterir', () => {
        serviceSpy.getPozisyon.and.returnValue(of(ornekSonuc()));

        const component = createComponent();
        component.ngOnInit();

        expect(component.sonuc?.kasaHesaplari.length).toBe(0);
        expect(component.sonuc?.bankaHesaplari.length).toBe(0);
        expect(component.errorMessage).toBeNull();
    });

    it('hata durumu: backend hatasi kullaniciya anlasilir bir mesajla bildirilir', () => {
        serviceSpy.getPozisyon.and.returnValue(throwError(() => new Error('Bu tesis için yetkiniz bulunmuyor.')));

        const component = createComponent();
        component.ngOnInit();

        expect(component.errorMessage).toContain('yetkiniz');
        expect(component.sonuc).toBeNull();
    });

    it('gecmis tarih raporu: gecmisTarihRaporuMu bayragi state uzerinden okunabilir', () => {
        serviceSpy.getPozisyon.and.returnValue(of(ornekSonuc({ gecmisTarihRaporuMu: true })));

        const component = createComponent();
        component.ngOnInit();

        expect(component.sonuc?.gecmisTarihRaporuMu).toBeTrue();
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

    it('satir detayinin acilmasi: openValorTakvimi valor takvimini (yalnizca gun ozetleri) yukler', () => {
        serviceSpy.getPozisyon.and.returnValue(of(ornekSonuc()));
        serviceSpy.getValorTakvimi.and.returnValue(
            of({
                kasaBankaHesapId: 5,
                raporTarihi: '2026-07-24',
                gunler: [{ valorTarihi: '2026-07-24', islemSayisi: 2, brutTutar: 200, komisyonTutari: 4, netTutar: 196 }]
            })
        );

        const component = createComponent();
        component.openValorTakvimi(ornekBankaHesabi());

        expect(component.takvimVisible).toBeTrue();
        expect(component.takvimData?.gunler.length).toBe(1);
        expect(component.isGunAcik('2026-07-24')).toBeFalse();
    });

    it('gun acma: toggleGun ilk acilista sayfali gun detaylarini yukler', () => {
        serviceSpy.getPozisyon.and.returnValue(of(ornekSonuc()));
        serviceSpy.getValorTakvimi.and.returnValue(
            of({ kasaBankaHesapId: 5, raporTarihi: '2026-07-24', gunler: [{ valorTarihi: '2026-07-24', islemSayisi: 1, brutTutar: 100, komisyonTutari: 2, netTutar: 98 }] })
        );
        serviceSpy.getValorGunDetaylari.and.returnValue(
            of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false })
        );

        const component = createComponent();
        component.openValorTakvimi(ornekBankaHesabi());
        component.toggleGun('2026-07-24');

        expect(component.isGunAcik('2026-07-24')).toBeTrue();
        expect(serviceSpy.getValorGunDetaylari).toHaveBeenCalledWith(5, '2026-07-24', 1, 25);

        component.toggleGun('2026-07-24');
        expect(component.isGunAcik('2026-07-24')).toBeFalse();
    });
});

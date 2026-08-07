import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { EBelgeYonetimi } from './e-belge-yonetimi';
import { EBelgeYonetimiService } from './e-belge-yonetimi.service';
import { KurumService } from '../../kurum-yonetimi/kurum.service';
import { AuthService } from '../../auth';
import { EBelgeEntegrasyonYontemi, KurumEBelgeReadinessModel, EBelgeYontemReadinessModel } from './e-belge-yonetimi.dto';

/**
 * Faz 2B.11 görev md.41 - readiness'in frontend'de YENİDEN HESAPLANMADIĞINI (yalnız
 * görselleştirildiğini), desteklenmeyen yöntemlerin disabled göründüğünü, GİB Portal için
 * signing gate'in kırmızı blokaj OLARAK gösterilmediğini, boş değişiklik nedeninin save'i
 * ENGELLEDİĞİNİ ve 409 sonrası state'in yeniden yüklendiğini kanıtlar.
 */
describe('EBelgeYonetimi', () => {
    let fixture: ComponentFixture<EBelgeYonetimi>;
    let component: EBelgeYonetimi;
    let serviceStub: {
        getPolitika: jasmine.Spy;
        getReadiness: jasmine.Spy;
        getRevizyonlar: jasmine.Spy;
        updatePolitika: jasmine.Spy;
    };

    function yontem(overrides: Partial<EBelgeYontemReadinessModel>): EBelgeYontemReadinessModel {
        return {
            yontem: EBelgeEntegrasyonYontemi.Kullanilmayacak,
            operasyonelMi: true,
            hariciSistemKoduGerekliMi: false,
            yerelSnapshotOlustur: false,
            yerelUnsignedUblOlustur: false,
            yerelImzaOlustur: false,
            otomatikGonderimYap: false,
            ...overrides
        };
    }

    function ornekReadiness(overrides?: Partial<KurumEBelgeReadinessModel>): KurumEBelgeReadinessModel {
        return {
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
            yerelImzaGerekliMi: false,
            otomatikGonderimGerekliMi: false,
            saticiAnaVerileriHazirMi: true,
            eksikSaticiAlanlari: [],
            signingGateUygulanabilirMi: false,
            signingSuAnMumkunMu: false,
            islemeHazirMi: true,
            blokajNedenleri: [],
            yontemler: [
                yontem({ yontem: EBelgeEntegrasyonYontemi.Kullanilmayacak, operasyonelMi: true }),
                yontem({ yontem: EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi, operasyonelMi: true, hariciSistemKoduGerekliMi: true }),
                yontem({ yontem: EBelgeEntegrasyonYontemi.GibPortal, operasyonelMi: true, yerelSnapshotOlustur: true, yerelUnsignedUblOlustur: true }),
                yontem({ yontem: EBelgeEntegrasyonYontemi.OzelEntegrator, operasyonelMi: false }),
                yontem({ yontem: EBelgeEntegrasyonYontemi.DogrudanGib, operasyonelMi: false })
            ],
            ...overrides
        };
    }

    function createComponent(readiness = ornekReadiness()): EBelgeYonetimi {
        serviceStub = {
            getPolitika: jasmine.createSpy('getPolitika').and.returnValue(of(null)),
            getReadiness: jasmine.createSpy('getReadiness').and.returnValue(of(readiness)),
            getRevizyonlar: jasmine.createSpy('getRevizyonlar').and.returnValue(of([])),
            updatePolitika: jasmine.createSpy('updatePolitika').and.returnValue(of({}))
        };

        TestBed.configureTestingModule({
            providers: [
                { provide: EBelgeYonetimiService, useValue: serviceStub },
                { provide: KurumService, useValue: { getById: () => of({ id: 7, ad: 'Test Kurum' }), getAll: () => of([]) } },
                {
                    provide: AuthService,
                    useValue: {
                        getAktifKurumId: () => 7,
                        isSuperAdminUser: () => false,
                        hasPermission: () => true,
                        getKurumIds: () => [7],
                        isKurumAdminFor: () => true
                    }
                },
                { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } }
            ]
        });
        fixture = TestBed.createComponent(EBelgeYonetimi);
        component = fixture.componentInstance;
        component.ngOnInit();
        return component;
    }

    function messageServiceOf(): MessageService {
        return fixture.debugElement.injector.get(MessageService);
    }

    it('desteklenmeyen yontemler (OzelEntegrator/DogrudanGib) secenek listesinde disabled gorunur', () => {
        createComponent();

        const ozelEntegrator = component.yontemSecenekleri.find((y) => y.value === EBelgeEntegrasyonYontemi.OzelEntegrator);
        const dogrudanGib = component.yontemSecenekleri.find((y) => y.value === EBelgeEntegrasyonYontemi.DogrudanGib);
        const gibPortal = component.yontemSecenekleri.find((y) => y.value === EBelgeEntegrasyonYontemi.GibPortal);

        expect(ozelEntegrator?.disabled).toBe(true);
        expect(dogrudanGib?.disabled).toBe(true);
        expect(gibPortal?.disabled).toBe(false);
    });

    it('HariciMuhasebeSistemi disinda hariciSistemKoduGerekliMi false doner', () => {
        createComponent();

        component.formDraft.entegrasyonYontemi = EBelgeEntegrasyonYontemi.GibPortal;
        expect(component.hariciSistemKoduGerekliMi).toBe(false);

        component.formDraft.entegrasyonYontemi = EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi;
        expect(component.hariciSistemKoduGerekliMi).toBe(true);
    });

    it('GIB Portal + signing gate uygulanamaz iken Imzalama karti "Uygulanamaz" (secondary) gosterir, kirmizi/danger DEGIL', () => {
        createComponent(ornekReadiness({ signingGateUygulanabilirMi: false, signingSuAnMumkunMu: false }));

        const imzalamaKarti = component.statusCards.find((c) => c.baslik === 'İmzalama');

        expect(imzalamaKarti?.durum).toBe('Uygulanamaz');
        expect(imzalamaKarti?.severity).toBe('secondary');
        expect(imzalamaKarti?.severity).not.toBe('danger');
    });

    it('degisiklik nedeni bos iken save() HTTP cagrisi YAPMAZ ve uyari gosterir', () => {
        createComponent();
        const messageService = messageServiceOf();
        spyOn(messageService, 'add');

        component.formDraft.degisiklikNedeni = '   ';
        component.formDraft.entegrasyonYontemi = EBelgeEntegrasyonYontemi.Kullanilmayacak;
        component.formDraft.aktifMi = false;

        component.save();

        expect(serviceStub.updatePolitika).not.toHaveBeenCalled();
        expect(messageService.add).toHaveBeenCalled();
    });

    it('409 concurrency hatasi sonrasi policy/readiness/revizyonlar YENIDEN yuklenir', () => {
        createComponent();
        serviceStub.getPolitika.calls.reset();
        serviceStub.getReadiness.calls.reset();
        serviceStub.getRevizyonlar.calls.reset();

        const conflict = new HttpErrorResponse({ status: 409, error: { message: 'Politika değişti.' } });
        (component as unknown as { handleSaveError(e: unknown): void }).handleSaveError(conflict);

        expect(serviceStub.getPolitika).toHaveBeenCalledTimes(1);
        expect(serviceStub.getReadiness).toHaveBeenCalledTimes(1);
        expect(serviceStub.getRevizyonlar).toHaveBeenCalledTimes(1);
    });

    it('canManage false (read-only kullanici) iken save() HTTP cagrisi YAPMAZ', () => {
        serviceStub = {
            getPolitika: jasmine.createSpy('getPolitika').and.returnValue(of(null)),
            getReadiness: jasmine.createSpy('getReadiness').and.returnValue(of(ornekReadiness())),
            getRevizyonlar: jasmine.createSpy('getRevizyonlar').and.returnValue(of([])),
            updatePolitika: jasmine.createSpy('updatePolitika').and.returnValue(of({}))
        };

        TestBed.configureTestingModule({
            providers: [
                { provide: EBelgeYonetimiService, useValue: serviceStub },
                { provide: KurumService, useValue: { getById: () => of({ id: 7, ad: 'Test Kurum' }), getAll: () => of([]) } },
                {
                    provide: AuthService,
                    useValue: {
                        getAktifKurumId: () => 7,
                        isSuperAdminUser: () => false,
                        // Manage izni YOK - yalnizca View.
                        hasPermission: (perm: string) => perm === 'MuhasebeSatisBelgeleriYonetimi.View',
                        getKurumIds: () => [7],
                        isKurumAdminFor: () => false
                    }
                },
                { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } }
            ]
        });
        fixture = TestBed.createComponent(EBelgeYonetimi);
        component = fixture.componentInstance;
        component.ngOnInit();

        component.formDraft.degisiklikNedeni = 'gecerli neden';
        component.save();

        expect(component.canManage).toBe(false);
        expect(serviceStub.updatePolitika).not.toHaveBeenCalled();
    });
});

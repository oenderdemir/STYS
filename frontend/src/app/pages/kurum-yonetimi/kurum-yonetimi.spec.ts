import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { KurumYonetimi } from './kurum-yonetimi';
import { KurumService } from './kurum.service';
import { KurumKullaniciService } from './kurum-kullanici.service';
import { KullaniciYonetimiService } from '../kullanici-yonetimi/kullanici-yonetimi.service';
import { AuthService } from '../auth';
import { KurumModel } from './kurum.model';

/**
 * Faz 2B.11 görev md.22 - `VergiDairesi`/`Adres`/`Ilce`/`Il` alanları frontend modelinde
 * OLMADIĞI için edit-save akışında sessizce kaybolabiliyordu (cloneKurum/normalizeKurumRequest
 * bu alanları KOPYALAMIYORDU). Bu test, GERÇEK "kurum yükle -> başka bir alan değiştir -> save"
 * akışını (component'in KENDİ save metodu üzerinden) çalıştırarak bu dört alanın PUT payload'ına
 * DEĞİŞMEDEN gittiğini kanıtlar.
 */
describe('KurumYonetimi — Mali/E-Belge alanlari edit-save sirasinda kaybolmaz', () => {
    let fixture: ComponentFixture<KurumYonetimi>;
    let updateSpy: jasmine.Spy;

    function ornekKurum(overrides?: Partial<KurumModel>): KurumModel {
        return {
            id: 7,
            kod: 'TRT',
            ad: 'Test Kurumu',
            vergiNo: '1111111111',
            vergiDairesi: 'Kadıköy Vergi Dairesi',
            adres: 'Test Sokak No:1',
            ilce: 'Kadıköy',
            il: 'İstanbul',
            telefon: '0212 000 00 00',
            eposta: 'info@test.com',
            aktifMi: true,
            tenantKey: 'trt',
            loginHost: 'trt.stys.com',
            ...overrides
        };
    }

    function createComponent(): KurumYonetimi {
        updateSpy = jasmine.createSpy('update').and.returnValue(of(ornekKurum()));

        TestBed.configureTestingModule({
            providers: [
                {
                    provide: KurumService,
                    useValue: {
                        update: updateSpy,
                        getMyKurumlar: () => of([]),
                        getAll: () => of([])
                    }
                },
                { provide: KurumKullaniciService, useValue: { getByKurum: () => of([]) } },
                { provide: KullaniciYonetimiService, useValue: { getUsers: () => of([]) } },
                {
                    provide: AuthService,
                    useValue: {
                        hasPermission: () => true,
                        isSuperAdminUser: () => true,
                        isKurumAdminFor: () => true,
                        getAktifKurumId: () => 7
                    }
                },
                { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } }
            ]
        });
        fixture = TestBed.createComponent(KurumYonetimi);
        return fixture.componentInstance;
    }

    it('kurum secilip yalnizca ad degistirilip kaydedildiginde VergiDairesi/Adres/Ilce/Il PUT payloadinda AYNEN korunur', () => {
        const component = createComponent();
        const kurum = ornekKurum();

        component.selectKurum(kurum);
        component.selectedKurum.ad = 'Test Kurumu (Guncellendi)';

        component.saveKurum();

        expect(updateSpy).toHaveBeenCalledTimes(1);
        const payload = updateSpy.calls.mostRecent().args[1];
        expect(payload.vergiDairesi).toBe('Kadıköy Vergi Dairesi');
        expect(payload.adres).toBe('Test Sokak No:1');
        expect(payload.ilce).toBe('Kadıköy');
        expect(payload.il).toBe('İstanbul');
        expect(payload.ad).toBe('Test Kurumu (Guncellendi)');
    });
});

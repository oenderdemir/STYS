import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { CariKartlarPage } from './cari-kartlar';
import { CariKartlarService } from './cari-kartlar.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { CARI_TIPLERI, CariKartModel } from './cari-kartlar.dto';

/**
 * `CariKartlarPage` şablonu, KENDİ içinde `<app-muhasebe-tesis-secim-dialog />` / `<app-muhasebe-
 * tesis-context-bar />` alt bileşenlerini render eder - bunlar da AYNI `MuhasebeTesisContextService`
 * enjeksiyonunu kullanır ve `tesisler`/`tesisSecenekleri` gibi DİĞER sinyalleri okur. Bu yüzden
 * sahte servis, testin KENDİ ihtiyacından (seciliTesis/requireSeciliTesisId) FAZLASINI - alt
 * bileşenlerin ÇÖKMEDEN render edebilmesi için gereken TÜM public sinyalleri - taşımalıdır.
 */
function createTesisContextStub() {
    return {
        seciliTesis: signal({ id: 1, ad: 'Test Tesis' }),
        tesisler: signal([{ id: 1, ad: 'Test Tesis' }]),
        tesislerLoading: signal(false),
        tesislerError: signal<string | null>(null),
        tesisSecenekleri: signal([{ label: 'Test Tesis', value: 1 }]),
        initialize: () => of([]),
        selectTesis: () => {},
        clearTesis: () => {},
        clearPersistedTesis: () => {},
        requireSeciliTesis: () => ({ id: 1, ad: 'Test Tesis' }),
        requireSeciliTesisId: () => 1
    };
}

/**
 * Faz 2B.11 görev md.23/md.44 - backend `Ad`/`Soyad` alanlarını ZATEN destekliyordu ama frontend
 * modeli/save payload'ı bunları TAŞIMIYORDU. Bu testler, (1) `save()`'in gerçek kişi (Musteri)
 * cari'de `ad`/`soyad`'ı payload'a KOYDUĞUNU ve (2) `openEdit` -> `mapToModel` akışının bunları
 * DEĞİŞTİRMEDEN KORUDUĞUNU kanıtlar - yeni bir DB alanı/migration GEREKMEZ (backend zaten hazır).
 */
describe('CariKartlarPage — Ad/Soyad alanlari API payload/reload akisinda korunur', () => {
    let fixture: ComponentFixture<CariKartlarPage>;

    function ornekCariKart(overrides?: Partial<CariKartModel>): CariKartModel {
        return {
            id: 3,
            tesisId: 1,
            cariTipi: CARI_TIPLERI.Musteri,
            cariKodu: 'MUS-001',
            unvanAdSoyad: 'Ahmet Yilmaz',
            ad: 'Ahmet',
            soyad: 'Yilmaz',
            vergiNoTckn: '11111111111',
            vergiDairesi: null,
            telefon: null,
            eposta: null,
            adres: null,
            il: null,
            ilce: null,
            aktifMi: true,
            eFaturaMukellefiMi: false,
            eArsivKapsamindaMi: false,
            aciklama: null,
            bankaHesaplari: [],
            yetkiliKisiler: [],
            ...overrides
        };
    }

    function createComponent(getByIdReturn: CariKartModel): { component: CariKartlarPage; createSpy: jasmine.Spy } {
        const createSpy = jasmine.createSpy('create').and.returnValue(of(ornekCariKart()));

        TestBed.configureTestingModule({
            providers: [
                {
                    provide: CariKartlarService,
                    useValue: {
                        create: createSpy,
                        getById: () => of(getByIdReturn),
                        getPaged: () => of({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 })
                    }
                },
                { provide: MuhasebeTesisContextService, useValue: createTesisContextStub() }
            ]
        });
        fixture = TestBed.createComponent(CariKartlarPage);
        return { component: fixture.componentInstance, createSpy };
    }

    it('gercek kisi (Musteri) carisi kaydedilirken ad/soyad payload alanlarina gider', () => {
        const { component, createSpy } = createComponent(ornekCariKart());
        component.dialogMode = 'create';
        component.model = ornekCariKart({ id: null });

        component.save();

        expect(createSpy).toHaveBeenCalledTimes(1);
        const payload = createSpy.calls.mostRecent().args[0];
        expect(payload.ad).toBe('Ahmet');
        expect(payload.soyad).toBe('Yilmaz');
    });

    it('openEdit sonrasi mapToModel ad/soyad alanlarini DEGISTIRMEDEN korur', () => {
        const duzenlenecek = ornekCariKart({ ad: 'Mehmet', soyad: 'Demir' });
        const { component } = createComponent(duzenlenecek);

        component.openEdit(duzenlenecek);

        expect(component.model.ad).toBe('Mehmet');
        expect(component.model.soyad).toBe('Demir');
    });

    it('isGercekKisi yalnizca CariTipi=Musteri icin true doner (KurumsalMusteri DAHIL digerleri kurumsal sayilir - backend ApplyCariSnapshot ile AYNI kural)', () => {
        const { component } = createComponent(ornekCariKart());

        component.model.cariTipi = CARI_TIPLERI.Musteri;
        expect(component.isGercekKisi()).toBe(true);

        component.model.cariTipi = CARI_TIPLERI.KurumsalMusteri;
        expect(component.isGercekKisi()).toBe(false);

        component.model.cariTipi = CARI_TIPLERI.Tedarikci;
        expect(component.isGercekKisi()).toBe(false);
    });
});

import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { StokHareketleriPage } from './stok-hareketleri';
import { StokHareketleriService } from './stok-hareketleri.service';
import { DepolarService } from '../depolar/depolar.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { MuhasebeFisService } from '../services/muhasebe-fis.service';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';

describe('StokHareketleriPage varsayilan depo davranisi', () => {
    function createComponent(): StokHareketleriPage {
        TestBed.configureTestingModule({
            providers: [
                {
                    provide: StokHareketleriService,
                    useValue: {
                        getPaged: () => of({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0 }),
                        getStokBakiye: () => of([]),
                        getStokKartOzet: () => of([]),
                        create: () => of({}),
                        update: () => of({})
                    }
                },
                { provide: DepolarService, useValue: { getAll: () => of([]) } },
                { provide: TasinirKartlariService, useValue: { getAll: () => of([]) } },
                { provide: CariKartlarService, useValue: { getAll: () => of([]) } },
                { provide: MuhasebeFisService, useValue: { getByKaynak: () => of([]) } },
                { provide: KdvIstisnaTanimService, useValue: { filter: () => of([]) } },
                {
                    provide: MuhasebeTesisContextService,
                    useValue: {
                        initialize: () => of(void 0),
                        seciliTesis: () => ({ id: 1, ad: 'Tesis 1' }),
                        requireSeciliTesisId: () => 1
                    }
                }
            ]
        });

        return TestBed.createComponent(StokHareketleriPage).componentInstance;
    }

    it('tasinir kart secilince varsayilan depoyu onerir', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Temizlik Deposu', value: 5 },
            { label: 'Ana Depo', value: 9 }
        ];
        (component as any).tasinirKartByIdMap = new Map([
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(5);
    });

    it('kullanici depoyu manuel degistirdikten sonra ayni kart icin tekrar varsayilana donmez', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Temizlik Deposu', value: 5 },
            { label: 'Ana Depo', value: 9 }
        ];
        (component as any).tasinirKartByIdMap = new Map([
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);
        component.onDepoChange(9);
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(9);
    });

    it('varsayilan deposu olmayan kartta depo secimi bos kalir', () => {
        const component = createComponent();
        component.depoOptions = [{ label: 'Ana Depo', value: 9 }];
        (component as any).tasinirKartByIdMap = new Map([
            [200, { id: 200, tesisId: 1, tasinirKodId: 2, varsayilanDepoId: null, stokKodu: 'STK-2', ad: 'Bos Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(200);

        expect(component.model.depoId).toBe(0);
    });

    it('ekrandaki depo filtresi varsa varsayilan deponun onune gecer', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Temizlik Deposu', value: 5 },
            { label: 'Ana Depo', value: 9 }
        ];
        component.selectedDepoId = 9;
        (component as any).tasinirKartByIdMap = new Map([
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(9);
    });
});

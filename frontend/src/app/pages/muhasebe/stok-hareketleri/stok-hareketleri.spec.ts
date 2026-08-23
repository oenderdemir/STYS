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
                        getLotBakiyeleri: () => of([]),
                        getSeriBakiyeleri: () => of([]),
                        create: () => of({}),
                        createTransfer: () => of([]),
                        update: () => of({}),
                        delete: () => of(void 0),
                        transferIptal: () => of(void 0)
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
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
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
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);
        component.onDepoChange(9);
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(9);
    });

    it('kullanici baska bir tasinir karta gecince yeni kartin varsayilan deposunu uygular', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Temizlik Deposu', value: 5 },
            { label: 'Ana Depo', value: 9 },
            { label: 'Yedek Depo', value: 12 }
        ];
        (component as any).tasinirKartByIdMap = new Map([
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Kart A', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }],
            [200, { id: 200, tesisId: 1, tasinirKodId: 2, varsayilanDepoId: 12, stokKodu: 'STK-2', ad: 'Kart B', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);
        component.onDepoChange(9);
        component.onTasinirKartChange(200);

        expect(component.model.depoId).toBe(12);
    });

    it('varsayilan deposu olmayan kartta depo secimi bos kalir', () => {
        const component = createComponent();
        component.depoOptions = [{ label: 'Ana Depo', value: 9 }];
        (component as any).tasinirKartByIdMap = new Map([
            [200, { id: 200, tesisId: 1, tasinirKodId: 2, varsayilanDepoId: null, stokKodu: 'STK-2', ad: 'Bos Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
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
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Finish Quantum', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(9);
    });

    it('transfer secilince hedef depo alani aktif olur', () => {
        const component = createComponent();

        component.openCreate();
        component.model.hareketTipi = 'Transfer';
        component.onHareketTipiChange();

        expect(component.showHedefDepoField()).toBeTrue();
    });

    it('transfer secilince durum alani gosterilmez', () => {
        const component = createComponent();

        component.openCreate();
        component.model.hareketTipi = 'Transfer';
        component.onHareketTipiChange();

        expect(component.showDurumField()).toBeFalse();
    });

    it('transferte kaynak depo varsayilan depo mantigindan gelir', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Ana Depo', value: 5 },
            { label: 'Yedek Depo', value: 9 }
        ];
        (component as any).tasinirKartByIdMap = new Map([
            [100, { id: 100, tesisId: 1, tasinirKodId: 1, varsayilanDepoId: 5, stokKodu: 'STK-1', ad: 'Transfer Karti', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: false, takipTipi: 'Yok', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.model.hareketTipi = 'Transfer';
        component.onHareketTipiChange();
        component.onTasinirKartChange(100);

        expect(component.model.depoId).toBe(5);
        expect(component.model.hedefDepoId).toBeNull();
    });

    it('transferte hedef depo kaynak depo olamaz', () => {
        const component = createComponent();
        component.depoOptions = [
            { label: 'Ana Depo', value: 5 },
            { label: 'Yedek Depo', value: 9 },
            { label: 'Mutfak Deposu', value: 12 }
        ];

        component.openCreate();
        component.model.hareketTipi = 'Transfer';
        component.onHareketTipiChange();
        component.onDepoChange(9);

        expect(component.getHedefDepoOptions().map(x => x.value)).toEqual([5, 12]);
        expect(component.getHedefDepoOptions().some(x => x.value === 9)).toBeFalse();
    });

    it('sayim farki secilince varsayilan yon fazla olur', () => {
        const component = createComponent();

        component.openCreate();
        component.model.hareketTipi = 'SayimFarki';
        component.onHareketTipiChange();

        expect(component.model.sayimFarkiYonu).toBe('Fazla');
        expect(component.model.kdvUygulamaTipi).toBe(4);
        expect(component.model.kdvOrani).toBe(0);
        expect(component.model.kdvIstisnaTanimId).toBeNull();
        expect(component.isSayimFarki(component.model)).toBeTrue();
    });

    it('takipli kart secilince giris icin lot alani acilir', () => {
        const component = createComponent();
        (component as any).tasinirKartByIdMap = new Map([
            [300, { id: 300, tesisId: 1, tasinirKodId: 3, varsayilanDepoId: 5, stokKodu: 'STK-300', ad: 'Takipli Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: true, takipTipi: 'Lot', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(300);

        expect(component.isTrackedSelectedCard()).toBeTrue();
        expect(component.isLotEntryMode()).toBeTrue();
        expect(component.isLotSelectionMode()).toBeFalse();
    });

    it('cikis ve transfer icin sadece pozitif bakiyeli lotlari kullanir', () => {
        const component = createComponent();
        (component as any).tasinirKartByIdMap = new Map([
            [300, { id: 300, tesisId: 1, tasinirKodId: 3, varsayilanDepoId: 5, stokKodu: 'STK-300', ad: 'Takipli Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: true, takipTipi: 'Lot', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(300);
        component.model.hareketTipi = 'Cikis';
        component.onHareketTipiChange();
        component.lotBakiyeOptions = [
            { stokLotId: 1, lotNo: 'LOT-A', sonKullanmaTarihi: '2027-01-01', girisMiktari: 10, cikisMiktari: 7, bakiyeMiktari: 3 },
            { stokLotId: 2, lotNo: 'LOT-B', sonKullanmaTarihi: '2027-02-01', girisMiktari: 5, cikisMiktari: 5, bakiyeMiktari: 0 }
        ];

        expect(component.isLotSelectionMode()).toBeTrue();
        expect(component.getPositiveLotOptions().map(x => x.value)).toEqual([1]);

        component.model.hareketTipi = 'Transfer';
        component.onHareketTipiChange();
        component.lotBakiyeOptions = [
            { stokLotId: 1, lotNo: 'LOT-A', sonKullanmaTarihi: '2027-01-01', girisMiktari: 10, cikisMiktari: 7, bakiyeMiktari: 3 },
            { stokLotId: 2, lotNo: 'LOT-B', sonKullanmaTarihi: '2027-02-01', girisMiktari: 5, cikisMiktari: 5, bakiyeMiktari: 0 }
        ];

        expect(component.isLotSelectionMode()).toBeTrue();
        expect(component.getPositiveLotOptions().map(x => x.value)).toEqual([1]);
    });

    it('seri takipli kart secilince seri alani acilir ve miktari bire sabitler', () => {
        const component = createComponent();
        (component as any).tasinirKartByIdMap = new Map([
            [400, { id: 400, tesisId: 1, tasinirKodId: 4, varsayilanDepoId: 5, stokKodu: 'STK-400', ad: 'Seri Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: true, takipTipi: 'Seri', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.model.miktar = 4;
        component.onTasinirKartChange(400);

        expect(component.isSeriEntryMode()).toBeTrue();
        expect(component.isLotEntryMode()).toBeFalse();
        expect(component.model.miktar).toBe(1);
    });

    it('seri takipli kartta cikis ve transfer icin depo serileri kullanilir', () => {
        const component = createComponent();
        (component as any).tasinirKartByIdMap = new Map([
            [400, { id: 400, tesisId: 1, tasinirKodId: 4, varsayilanDepoId: 5, stokKodu: 'STK-400', ad: 'Seri Kart', birim: 'Adet', malzemeTipi: 'Diger', sarfMi: false, demirbasMi: false, takipliMi: true, takipTipi: 'Seri', kdvOrani: 20, aktifMi: true }]
        ]);

        component.openCreate();
        component.onTasinirKartChange(400);
        component.model.hareketTipi = 'Cikis';
        component.onHareketTipiChange();
        component.seriBakiyeOptions = [{ stokSeriId: 7, seriNo: 'SN001' }];

        expect(component.isSeriSelectionMode()).toBeTrue();
        expect(component.getPositiveSeriOptions()).toEqual([{ label: 'SN001', value: 7 }]);
    });
});

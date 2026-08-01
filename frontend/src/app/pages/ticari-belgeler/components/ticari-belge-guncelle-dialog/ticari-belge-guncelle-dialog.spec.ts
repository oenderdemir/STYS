import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { TicariBelgeGuncelleDialogComponent } from './ticari-belge-guncelle-dialog';
import { TicariBelgeService } from '../../ticari-belge.service';
import {
    KdvUygulamaTipi,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    TicariBelgeGuncelleRequest,
    TicariBelgeGuncelleSatirRequest,
    TicariBelgeIadeAdayiDto,
    TicariBelgeKaynakSatirDto
} from '../../ticari-belge.models';

function ornekIadeSatiri(overrides?: Partial<TicariBelgeGuncelleSatirRequest>): TicariBelgeGuncelleSatirRequest {
    return {
        siraNo: 1,
        satirTipi: SatisBelgesiSatirTipi.Iade,
        aciklama: 'Kullanicinin girdigi aciklama',
        birim: 'Adet',
        miktar: 3,
        birimFiyat: 100,
        indirimOrani: 0,
        indirimTutari: 0,
        kdvUygulamaTipi: KdvUygulamaTipi.Kdvli,
        kdvIstisnaTanimId: null,
        kdvOrani: 20,
        tevkifatPay: null,
        tevkifatPayda: null,
        otvOrani: 0,
        otvTutari: 0,
        oivOrani: 0,
        oivTutari: 0,
        konaklamaVergisiOrani: 0,
        konaklamaVergisiTutari: 0,
        kaynakSatirId: '10',
        ...overrides
    };
}

function ornekFormData(overrides?: Partial<TicariBelgeGuncelleRequest>): TicariBelgeGuncelleRequest {
    return {
        belgeNo: 'BLG-1',
        belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
        tesisId: 1,
        cariKartId: 5,
        belgeTarihi: '2026-03-05',
        vadeTarihi: null,
        musteriUnvan: null,
        musteriAdSoyad: 'Test Musteri',
        musteriVergiNo: null,
        musteriTcKimlikNo: null,
        musteriVergiDairesi: null,
        musteriAdres: null,
        musteriEposta: null,
        musteriTelefon: null,
        kurumsalMi: false,
        aciklama: null,
        karsiTarafFaturaNo: null,
        iadeEdilenBelgeId: 50,
        iadeEdilenBelgeReferansiKaldir: false,
        satirlar: [ornekIadeSatiri()],
        ...overrides
    };
}

function ornekKaynakSatir(overrides?: Partial<TicariBelgeKaynakSatirDto>): TicariBelgeKaynakSatirDto {
    return {
        id: 10,
        aciklama: 'Kaynak aciklamasi',
        birim: 'Adet',
        miktar: 10,
        iadeEdilebilirKalanMiktar: 7,
        birimFiyat: 100,
        indirimOrani: 0,
        kdvUygulamaTipi: KdvUygulamaTipi.Kdvli,
        kdvOrani: 20,
        kdvIstisnaTanimId: null,
        tevkifatPay: null,
        tevkifatPayda: null,
        ...overrides
    };
}

const VISIBLE_ILK_ACILIS = { visible: { currentValue: true, previousValue: false, firstChange: true, isFirstChange: () => true } };

describe('TicariBelgeGuncelleDialogComponent - iade kaynak satir esleme', () => {
    let serviceSpy: jasmine.SpyObj<TicariBelgeService>;

    function createComponent(): TicariBelgeGuncelleDialogComponent {
        TestBed.configureTestingModule({
            providers: [{ provide: TicariBelgeService, useValue: serviceSpy }, MessageService]
        });
        return TestBed.createComponent(TicariBelgeGuncelleDialogComponent).componentInstance;
    }

    beforeEach(() => {
        serviceSpy = jasmine.createSpyObj<TicariBelgeService>('TicariBelgeService', [
            'getById',
            'getCariKartLookup',
            'getKdvIstisnaLookup',
            'getIadeAdaylari',
            'getKaynakSatirlar'
        ]);
        serviceSpy.getById.and.returnValue(of({ id: 50, belgeNo: 'ASIL-1', belgeTarihi: '2026-03-01' }) as never);
        serviceSpy.getCariKartLookup.and.returnValue(of([]));
        serviceSpy.getKdvIstisnaLookup.and.returnValue(of([]));
    });

    it('mevcut iade belgesi acilinca kayitli satir/miktar/aciklama korunur', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 7 })]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        expect(serviceSpy.getKaynakSatirlar).toHaveBeenCalledWith(50, 99);

        const satirlar = component.formData!.satirlar!;
        expect(satirlar.length).toBe(1);
        expect(satirlar[0].miktar).toBe(3);
        expect(satirlar[0].aciklama).toBe('Kullanicinin girdigi aciklama');
        expect(satirlar[0].kaynakSatirId).toBe('10');
        expect(component.kaynakSatirHataMesaji).toBeNull();
    });

    it('kaynakta fazladan bulunan (mevcut iadede karsiligi olmayan) satir otomatik eklenmez', () => {
        // Kaynak, mevcutta kayitli olan (id:10) satirin YANI SIRA, iadede HENUZ karsiligi olmayan
        // ikinci bir satir (id:11) da iceriyor - bu satir forma OTOMATIK EKLENMEMELIDIR.
        serviceSpy.getKaynakSatirlar.and.returnValue(
            of([ornekKaynakSatir({ id: 10 }), ornekKaynakSatir({ id: 11, aciklama: 'Kaynaktaki fazladan satir' })])
        );

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        const satirlar = component.formData!.satirlar!;
        expect(satirlar.length).toBe(1);
        expect(satirlar.some(s => s.kaynakSatirId === '11')).toBeFalse();
        expect(component.kaynakSatirHataMesaji).toBeNull();
    });

    it('kayitli bir satirin kaynak satiri artik bulunamiyorsa kaydetme engellenir ve acik hata gosterilir', () => {
        // Kaynak listesinde id:10 ARTIK yok (silinmis/degismis) - yalnizca alakasiz bir id donuyor.
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 99 })]));

        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        expect(component.kaynakSatirHataMesaji).toContain('bulunamadı');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('kayitli satirin kilitli mali alanlari (birim fiyat) kaynakla uyusmazsa kaydetme engellenir', () => {
        // Kaynak satir hala id:10 - ama artik birim fiyati kayitli satirdan (100) FARKLI (150).
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, birimFiyat: 150 })]));

        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        expect(component.kaynakSatirHataMesaji).toContain('uyumsuz');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('kayitli satirin mali alanlari kaynakla TAM uyumluysa kaydetme engellenmez', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10 })]));

        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        expect(component.kaynakSatirHataMesaji).toBeNull();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).toHaveBeenCalledTimes(1);
    });

    it('farkli bir iade kaynagi secilince eski satirlar HEMEN temizlenir ve satirlar sifirdan yeniden eslenir', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(
            of([ornekKaynakSatir({ id: 20, aciklama: 'Yeni kaynak satir A', miktar: 8, iadeEdilebilirKalanMiktar: 8 }), ornekKaynakSatir({ id: 21, aciklama: 'Yeni kaynak satir B', miktar: 4, iadeEdilebilirKalanMiktar: 4 })])
        );

        const component = createComponent();
        component.belgeId = 99;
        // Mevcut satirlar ESKI kaynagin (id:10) satirlarini iceriyor.
        component.formData = ornekFormData({ satirlar: [ornekIadeSatiri({ kaynakSatirId: '10', miktar: 6 })] });

        const yeniBelge: TicariBelgeIadeAdayiDto = { id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' };
        component.onIadeEdilenBelgeSecildi(yeniBelge);

        expect(serviceSpy.getKaynakSatirlar).toHaveBeenCalledWith(77, 99);

        const satirlar = component.formData!.satirlar!;
        expect(satirlar.length).toBe(2);
        // Eski kaynak (id:10) referansi HICBIR satirda kalmamis olmali.
        expect(satirlar.some(s => s.kaynakSatirId === '10')).toBeFalse();
        expect(satirlar.map(s => s.kaynakSatirId).sort()).toEqual(['20', '21']);
        expect(component.formData!.iadeEdilenBelgeId).toBe(77);
    });

    it('yeni kaynak secildiginde satirlar senkron olarak (istek sonucu beklenmeden) hemen bosaltilir', () => {
        // getKaynakSatirlar HENUZ cozulmemis (subscribe callback'i asenkron calisir) - satirlarin
        // SENKRON olarak, istek sonucundan BAGIMSIZ hemen temizlendigi dogrulanir.
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 20 })]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })] });

        // getKaynakSatirlar'i cagirmadan ONCE satirlarin durumunu yakalamak icin spy'i gecici
        // olarak "of" yerine hicbir sey yapmayan bir Observable ile degistiriyoruz.
        serviceSpy.getKaynakSatirlar.and.returnValue({ subscribe: () => undefined } as never);
        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' });

        expect(component.formData!.satirlar).toEqual([]);
    });

    it('yeni kaynak lookup istegi basarisiz olursa eski KaynakSatirId ile birlikte gonderim yapilamaz, hata gosterilir ve Kaydet engellenir', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(throwError(() => new Error('network error')));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [ornekIadeSatiri({ kaynakSatirId: '10', miktar: 6 })] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' });

        // Eski KaynakSatirId'ler (10) yeni referansla (77) BIRLIKTE gonderilemez - satirlar bos.
        expect(component.formData!.satirlar).toEqual([]);
        expect(component.formData!.iadeEdilenBelgeId).toBe(77);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });
});

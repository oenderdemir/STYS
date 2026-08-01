import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
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

    it('mevcut iade belgesi acilinca kayitli satir/miktar/aciklama korunur ve kaynakta olup eksik olan satir otomatik eklenir', () => {
        // Kaynak, mevcutta zaten kayitli olan (id:10) satirin YANI SIRA, belgeye SONRADAN eklenmis
        // ikinci bir kaynak satiri (id:11) da iceriyor.
        serviceSpy.getKaynakSatirlar.and.returnValue(
            of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 7 }), ornekKaynakSatir({ id: 11, aciklama: 'Yeni kaynak satiri', miktar: 5, iadeEdilebilirKalanMiktar: 5 })])
        );

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges({ visible: { currentValue: true, previousValue: false, firstChange: true, isFirstChange: () => true } });

        expect(serviceSpy.getKaynakSatirlar).toHaveBeenCalledWith(50, 99);

        const satirlar = component.formData!.satirlar!;
        // Kayitli satir (kaynakSatirId '10') MIKTAR/ACIKLAMA degismeden korunmus olmali.
        const korunanSatir = satirlar.find(s => s.kaynakSatirId === '10');
        expect(korunanSatir?.miktar).toBe(3);
        expect(korunanSatir?.aciklama).toBe('Kullanicinin girdigi aciklama');

        // Kaynakta olup mevcut satirlarda karsiligi olmayan satir (id:11) otomatik eklenmis olmali.
        const eklenenSatir = satirlar.find(s => s.kaynakSatirId === '11');
        expect(eklenenSatir).toBeTruthy();
        expect(eklenenSatir?.miktar).toBe(0);
        expect(eklenenSatir?.aciklama).toBe('Yeni kaynak satiri');

        expect(satirlar.length).toBe(2);
        expect(component.kaynakSatirBulunamadiMesaji).toBeNull();
    });

    it('kayitli bir satirin kaynak satiri artik bulunamiyorsa kaydetme engellenir ve acik hata gosterilir', () => {
        // Kaynak listesinde id:10 ARTIK yok (silinmis/degismis) - yalnizca alakasiz bir id donuyor.
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 99 })]));

        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges({ visible: { currentValue: true, previousValue: false, firstChange: true, isFirstChange: () => true } });

        expect(component.kaynakSatirBulunamadiMesaji).toContain('bulunamadı');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('farkli bir iade kaynagi secilince satirlar sifirdan yeniden eslenir ve eski KaynakSatirId tasinmaz', () => {
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
});

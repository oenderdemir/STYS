import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';
import { TicariBelgeGuncelleDialogComponent } from './ticari-belge-guncelle-dialog';
import { TicariBelgeService } from '../../ticari-belge.service';
import {
    KdvUygulamaTipi,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    TicariBelgeCariKartLookupDto,
    TicariBelgeDetayDto,
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

function ornekCariKart(overrides?: Partial<TicariBelgeCariKartLookupDto>): TicariBelgeCariKartLookupDto {
    return {
        id: 5,
        cariKodu: 'MUS-5',
        cariTipi: 'Musteri',
        unvanAdSoyad: 'Gercek Musteri',
        vergiNoTckn: '11111111111',
        vergiDairesi: 'Merkez',
        adres: 'Adres',
        eposta: 'musteri@ornek.com',
        telefon: '5551112233',
        kurumsalMi: false,
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

    it('kaynak satirlar yuklenirken (loading) Kaydet engellenir; yanit gelince loading kapanir', () => {
        const subject = new Subject<TicariBelgeKaynakSatirDto[]>();
        serviceSpy.getKaynakSatirlar.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' });

        expect(component.kaynakSatirlarYukleniyor).toBeTrue();
        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();

        subject.next([ornekKaynakSatir({ id: 77, iadeEdilebilirKalanMiktar: 5 })]);
        subject.complete();

        expect(component.kaynakSatirlarYukleniyor).toBeFalse();
    });

    it('eski (stale) kaynak istegi sonucu, kullanici bu arada baska bir kaynak sectiyse uygulanmaz', () => {
        const subjectA = new Subject<TicariBelgeKaynakSatirDto[]>();
        const subjectB = new Subject<TicariBelgeKaynakSatirDto[]>();
        serviceSpy.getKaynakSatirlar.and.returnValues(subjectA.asObservable(), subjectB.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'A', belgeTarihi: '2026-03-01' });
        component.onIadeEdilenBelgeSecildi({ id: 88, belgeNo: 'B', belgeTarihi: '2026-03-02' });

        // Eski (A) istegin yaniti simdi (gec) gelir - STALE, sessizce yok sayilmali.
        subjectA.next([ornekKaynakSatir({ id: 10, aciklama: 'A kaynak satiri', iadeEdilebilirKalanMiktar: 5 })]);

        expect(component.formData!.satirlar!.some(s => s.aciklama === 'A kaynak satiri')).toBeFalse();
        // B'nin istegi hala bekliyor - loading hala acik olmali.
        expect(component.kaynakSatirlarYukleniyor).toBeTrue();

        // Guncel (B) istegin yaniti gelir - UYGULANMALI.
        subjectB.next([ornekKaynakSatir({ id: 20, aciklama: 'B kaynak satiri', iadeEdilebilirKalanMiktar: 5 })]);

        expect(component.formData!.satirlar!.some(s => s.aciklama === 'B kaynak satiri')).toBeTrue();
        expect(component.formData!.iadeEdilenBelgeId).toBe(88);
        expect(component.kaynakSatirlarYukleniyor).toBeFalse();
    });

    it('eski (stale) mevcut-belge-acilisi lookup yaniti, kullanici bu arada farkli kaynak sectiyse uygulanmaz', () => {
        const subjectAcilis = new Subject<TicariBelgeKaynakSatirDto[]>();
        const subjectYeniSecim = new Subject<TicariBelgeKaynakSatirDto[]>();
        serviceSpy.getKaynakSatirlar.and.returnValues(subjectAcilis.asObservable(), subjectYeniSecim.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData();
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        // Dialog acilis lookup'i HENUZ cozulmeden kullanici FARKLI bir kaynak secer.
        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'YENI', belgeTarihi: '2026-03-02' });

        // Acilis istegi (eski/stale) simdi cozulur - artik gecerli olmayan bir hata mesaji
        // birakmamali (kaynakSatirHataMesaji uygulanmaz).
        subjectAcilis.error(new Error('eski istek hatasi'));
        expect(component.kaynakSatirHataMesaji).toBeNull();

        subjectYeniSecim.next([ornekKaynakSatir({ id: 20, aciklama: 'Yeni kaynak', iadeEdilebilirKalanMiktar: 5 })]);
        expect(component.formData!.satirlar!.some(s => s.aciklama === 'Yeni kaynak')).toBeTrue();
    });

    it('yeni kaynak eslemesinde iadeEdilebilirKalanMiktar=0 olan satirlar haric tutulur, digerleri dahil edilir', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(
            of([
                ornekKaynakSatir({ id: 30, aciklama: 'Tukenmis', iadeEdilebilirKalanMiktar: 0 }),
                ornekKaynakSatir({ id: 31, aciklama: 'Kalan var', iadeEdilebilirKalanMiktar: 3 })
            ])
        );

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-4', belgeTarihi: '2026-03-04' });

        const satirlar = component.formData!.satirlar!;
        expect(satirlar.length).toBe(1);
        expect(satirlar[0].kaynakSatirId).toBe('31');
        expect(component.kaynakSatirHataMesaji).toBeNull();
    });

    it('yeni kaynakta iadeEdilebilirKalanMiktar>0 hicbir satir yoksa acik hata gosterilir ve kaydetme engellenir', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 30, iadeEdilebilirKalanMiktar: 0 })]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-5', belgeTarihi: '2026-03-05' });

        expect(component.formData!.satirlar).toEqual([]);
        expect(component.kaynakSatirHataMesaji).toContain('iade edilebilir');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('arama metni yazarken (searchIadeEdilenBelge) satirlar/referans degismez, yalnizca oneriler guncellenir', () => {
        serviceSpy.getIadeAdaylari.and.returnValue(of([{ id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' }]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })] });

        // Kullanicinin arama kutusuna YAZDIGI her karakter (onSelect DEGIL, completeMethod) yalnizca
        // searchIadeEdilenBelge'yi tetikler - bu, GERCEK bir secim DEGILDIR.
        component.searchIadeEdilenBelge({ query: 'ASIL' });

        expect(component.iadeEdilenBelgeSuggestions.length).toBe(1);
        expect(component.formData!.satirlar!.length).toBe(1);
        expect(component.formData!.satirlar![0].kaynakSatirId).toBe('10');
        expect(component.formData!.iadeEdilenBelgeId).toBe(50);
        expect(serviceSpy.getKaynakSatirlar).not.toHaveBeenCalled();
    });

    it('resolveIadeEdilenBelgeGosterim gec gelen (stale) yaniti, kullanici bu arada baska kaynak sectiyse uygulanmaz', () => {
        const acilisGetById = new Subject<TicariBelgeDetayDto>();
        serviceSpy.getById.and.returnValue(acilisGetById.asObservable());
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 20, iadeEdilebilirKalanMiktar: 5 })]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ iadeEdilenBelgeId: 50 });
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        // Acilis gosterim lookup'i (getById(50)) HENUZ cozulmeden kullanici FARKLI bir kaynak secer.
        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'YENI', belgeTarihi: '2026-03-02' });

        // Eski (50) getById yaniti simdi (gec) gelir - STALE, gosterimi eski belgeye GERI DONDURMEMELI.
        acilisGetById.next({ id: 50, belgeNo: 'ESKI-BELGE', belgeTarihi: '2026-02-01' } as TicariBelgeDetayDto);

        expect(component.iadeEdilenBelgeGosterim?.id).toBe(77);
        expect(component.iadeEdilenBelgeGosterim?.belgeNo).toBe('YENI');
    });

    it('referansi kaldir: iadeEdilenBelgeId null olur, referansiKaldir true olur, satirlar bos gonderilir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })] });

        component.clearIadeEdilenBelgeReferansi();

        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.formData!.iadeEdilenBelgeReferansiKaldir).toBeTrue();
        expect(component.formData!.satirlar).toEqual([]);
        expect(component.iadeEdilenBelgeGosterim).toBeNull();
    });

    it('referans kaldirildiktan sonra bekleyen (stale) bir kaynak satir yaniti sessizce yok sayilir', () => {
        const subject = new Subject<TicariBelgeKaynakSatirDto[]>();
        serviceSpy.getKaynakSatirlar.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ satirlar: [] });

        component.onIadeEdilenBelgeSecildi({ id: 77, belgeNo: 'ASIL-2', belgeTarihi: '2026-03-02' });
        component.clearIadeEdilenBelgeReferansi();

        // Kaldirmadan ONCE baslatilan kaynak satir istegi simdi (gec) sonuclanir - eski
        // KaynakSatirId'ler ARTIK kaldirilmis referansla birlikte SESSIZCE geri GELMEMELI.
        subject.next([ornekKaynakSatir({ id: 20, aciklama: 'Gec gelen kaynak', iadeEdilebilirKalanMiktar: 5 })]);

        expect(component.formData!.satirlar).toEqual([]);
        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.kaynakSatirlarYukleniyor).toBeFalse();
    });

    it('belgeIadeTipiMi yalnizca gercek iade belge tiplerinde true doner (bkz. gorev 1)', () => {
        const component = createComponent();
        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi });
        expect(component.belgeIadeTipiMi()).toBeTrue();

        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.AlisIadeFaturasi });
        expect(component.belgeIadeTipiMi()).toBeTrue();

        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisFaturasi });
        expect(component.belgeIadeTipiMi()).toBeFalse();

        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.AlisFaturasi });
        expect(component.belgeIadeTipiMi()).toBeFalse();
    });

    it('normalden iadeye gecince eski satirlar temizlenir ve kaynak secilmeden kaydetme engellenir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            iadeEdilenBelgeId: null,
            satirlar: [{ ...ornekIadeSatiri({ kaynakSatirId: null }), satirTipi: SatisBelgesiSatirTipi.Urun }]
        });
        // oncekiBelgeTipi'yi (SatisFaturasi olarak) senkronize etmek icin ilk acilis tetiklenir.
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        component.onBelgeTipiChange(SatisBelgesiTipi.SatisIadeFaturasi);

        expect(component.formData!.satirlar).toEqual([]);
        expect(component.kaynakSatirHataMesaji).toContain('kaynak');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('iadeden normale gecince eski iade satirlari normal satira DONUSTURULMEZ - referansla birlikte tamamen temizlenir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10', miktar: 4, aciklama: 'Iade satiri' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        component.onBelgeTipiChange(SatisBelgesiTipi.SatisFaturasi);

        // Eski iade satiri KORUNMAZ/DONUSTURULMEZ - satirlar TAMAMEN bosaltilir.
        expect(component.formData!.satirlar).toEqual([]);
        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.formData!.iadeEdilenBelgeReferansiKaldir).toBeTrue();
        expect(component.iadeEdilenBelgeGosterim).toBeNull();
        expect(component.kaynakSatirlar).toEqual([]);
        // Kullanici YENI bir normal satir eklemeden kaydetme engellenir.
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('iadeden normale gecince yeni satir eklenebilir ve eklenince gecis hata mesaji kaydi engellemez', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        component.onBelgeTipiChange(SatisBelgesiTipi.SatisFaturasi);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        component.addSatir();

        expect(component.formData!.satirlar!.length).toBe(1);
        expect(component.formData!.satirlar![0].kaynakSatirId).toBeFalsy();
        expect(component.kaynakSatirHataMesaji).toBeNull();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).toHaveBeenCalledTimes(1);
    });

    it('normal bir belge hicbir satir icermeden kaydedilemez', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisFaturasi, iadeEdilenBelgeId: null, satirlar: [] });

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();

        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('normalden iadeye gecip sonra kaynak secilince kaydetme engeli kalkar', () => {
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 60, iadeEdilebilirKalanMiktar: 5 })]));

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            iadeEdilenBelgeId: null,
            satirlar: [{ ...ornekIadeSatiri({ kaynakSatirId: null }), satirTipi: SatisBelgesiSatirTipi.Urun }]
        });
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        component.onBelgeTipiChange(SatisBelgesiTipi.SatisIadeFaturasi);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        component.onIadeEdilenBelgeSecildi({ id: 60, belgeNo: 'ASIL-6', belgeTarihi: '2026-03-06' });

        expect(component.kaynakSatirHataMesaji).toBeNull();
        expect(component.formData!.satirlar!.length).toBe(1);

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).toHaveBeenCalledTimes(1);
    });

    it('SatisIadeFaturasi -> AlisIadeFaturasi gecisinde eski referans/KaynakSatirId/kaynak satirlar tamamen temizlenir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            cariKartId: 5,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        component.kaynakSatirlar = [ornekKaynakSatir({ id: 10 })];

        component.onBelgeTipiChange(SatisBelgesiTipi.AlisIadeFaturasi);

        expect(component.formData!.satirlar).toEqual([]);
        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.formData!.iadeEdilenBelgeReferansiKaldir).toBeTrue();
        expect(component.iadeEdilenBelgeGosterim).toBeNull();
        expect(component.kaynakSatirlar).toEqual([]);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('iade yonu degisikliginde bekleyen eski kaynak istegi gecersiz kilinir (stale yanit uygulanmaz)', () => {
        const subject = new Subject<TicariBelgeKaynakSatirDto[]>();
        serviceSpy.getKaynakSatirlar.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        expect(component.kaynakSatirlarYukleniyor).toBeTrue();

        component.onBelgeTipiChange(SatisBelgesiTipi.AlisIadeFaturasi);
        expect(component.kaynakSatirlarYukleniyor).toBeFalse();

        // Acilis anindaki (artik eskimis/stale) kaynak satir istegi simdi (gec) sonuclanir.
        subject.next([ornekKaynakSatir({ id: 10, aciklama: 'Eski (stale) kaynak', iadeEdilebilirKalanMiktar: 6 })]);

        expect(component.formData!.satirlar).toEqual([]);
        expect(component.kaynakSatirlarYukleniyor).toBeFalse();
    });

    it('iade yonu degisikliginden sonra yeni yona uygun kaynak secilince kaydetme engeli kalkar', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        component.onBelgeTipiChange(SatisBelgesiTipi.AlisIadeFaturasi);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 90, iadeEdilebilirKalanMiktar: 3 })]));
        component.onIadeEdilenBelgeSecildi({ id: 90, belgeNo: 'ALIS-ASIL', belgeTarihi: '2026-03-09' });

        expect(component.kaynakSatirHataMesaji).toBeNull();
        expect(component.formData!.satirlar!.length).toBe(1);
        expect(component.formData!.satirlar![0].kaynakSatirId).toBe('90');

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).toHaveBeenCalledTimes(1);
    });

    it('yazi yazma (filterCari/completeMethod) cariKartId veya musteri snapshot alanlarini DEGISTIRMEZ', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ cariKartId: 5, musteriAdSoyad: 'Gercek Musteri' });
        component.cariKartlar = [ornekCariKart()];

        // Kullanicinin arama kutusuna yazdigi metin yalnizca filterCari'yi (oneri listesini)
        // tetikler - onCariKartSecildi'yi ASLA cagirmamalidir (bkz. gorev 1).
        component.filterCari({ query: 'gercek' });

        expect(component.formData!.cariKartId).toBe(5);
        expect(component.formData!.musteriAdSoyad).toBe('Gercek Musteri');
        expect(component.filteredCariKartlar.length).toBe(1);
    });

    it('cari onClear (temizleme) ile secim gercekten temizlenir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ cariKartId: 5, musteriAdSoyad: 'Gercek Musteri' });
        component.selectedCari = ornekCariKart();

        component.onCariKartSecildi(null);

        expect(component.formData!.cariKartId).toBeNull();
        expect(component.formData!.musteriAdSoyad).toBeNull();
        expect(component.selectedCari).toBeNull();
    });

    it('selectedCari henuz yuklenmemisken (asenkron lookup surerken) belge yonu degisirse mevcut cari yine de temizlenir', () => {
        const component = createComponent();
        component.belgeId = 99;
        // Cari lookup HENUZ cozulmemis - selectedCari hala null, ama formData.cariKartId
        // GERCEK (kayitli) bir cariyi gosteriyor.
        serviceSpy.getCariKartLookup.and.returnValue({ subscribe: () => undefined } as never);
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.AlisFaturasi,
            cariKartId: 7,
            musteriAdSoyad: 'Tedarikci Cari',
            iadeEdilenBelgeId: null,
            satirlar: []
        });
        // oncekiBelgeTipi'yi (AlisFaturasi olarak) senkronize etmek icin ilk acilis tetiklenir -
        // cari lookup yaniti HENUZ gelmedigi icin selectedCari null KALIR.
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        expect(component.selectedCari).toBeNull();

        component.onBelgeTipiChange(SatisBelgesiTipi.SatisFaturasi);

        // selectedCari yuklenmedigi (uygunluk KANITLANAMADIGI) icin GUVENLI tarafta kalinip
        // cari HER HALUKARDA temizlenir (bkz. gorev 2).
        expect(component.formData!.cariKartId).toBeNull();
        expect(component.formData!.musteriAdSoyad).toBeNull();
    });

    it('cari kart lookup stale yaniti yeni belge tipinin listesini EZMEZ', () => {
        const subjectEski = new Subject<TicariBelgeCariKartLookupDto[]>();
        const subjectYeni = new Subject<TicariBelgeCariKartLookupDto[]>();
        serviceSpy.getCariKartLookup.and.returnValues(subjectEski.asObservable(), subjectYeni.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            cariKartId: null,
            iadeEdilenBelgeId: null,
            satirlar: []
        });
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS); // ESKI (1.) istek baslar

        component.onBelgeTipiChange(SatisBelgesiTipi.AlisFaturasi); // YENI (2.) istek baslar

        // YENI istek ONCE cozulur.
        const yeniListe = [ornekCariKart({ id: 20, cariTipi: 'Tedarikci', unvanAdSoyad: 'Yeni Tedarikci' })];
        subjectYeni.next(yeniListe);

        // ESKI istek DAHA SONRA (gec) cozulur - YENI listenin UZERINE YAZMAMALI.
        const eskiListe = [ornekCariKart({ id: 5, cariTipi: 'Musteri', unvanAdSoyad: 'Eski Musteri' })];
        subjectEski.next(eskiListe);

        expect(component.cariKartlar).toEqual(yeniListe);
    });

    it('iade adayi arama stale yaniti eski sorgunun sonucunu gostermez', () => {
        const subjectEski = new Subject<TicariBelgeIadeAdayiDto[]>();
        const subjectYeni = new Subject<TicariBelgeIadeAdayiDto[]>();
        serviceSpy.getIadeAdaylari.and.returnValues(subjectEski.asObservable(), subjectYeni.asObservable());

        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi, cariKartId: 5, iadeEdilenBelgeId: null });

        component.searchIadeEdilenBelge({ query: 'ABC' }); // ESKI (1.) arama
        component.searchIadeEdilenBelge({ query: 'ABCD' }); // YENI (2.) arama

        const yeniSonuc = [{ id: 91, belgeNo: 'YENI-91', belgeTarihi: '2026-04-01' }];
        subjectYeni.next(yeniSonuc);

        const eskiSonuc = [{ id: 90, belgeNo: 'ESKI-90', belgeTarihi: '2026-03-01' }];
        subjectEski.next(eskiSonuc); // gec gelen ESKI yanit - YOK SAYILMALI

        expect(component.iadeEdilenBelgeSuggestions).toEqual(yeniSonuc);
    });

    it('belge tarihi degisince eski iade adayi onerileri temizlenir ve mevcut referans+kaynak satirlar birlikte silinir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        component.iadeEdilenBelgeSuggestions = [{ id: 77, belgeNo: 'ESKI-ONERI', belgeTarihi: '2026-03-01' }];

        component.onBelgeTarihiChange(new Date(2026, 2, 20));

        expect(component.iadeEdilenBelgeSuggestions).toEqual([]);
        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.formData!.satirlar).toEqual([]);
        expect(component.kaynakSatirlar).toEqual([]);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('cari degisince eski iade adayi onerileri temizlenir ve mevcut referans+kaynak satirlar birlikte silinir', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            cariKartId: 5,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        component.iadeEdilenBelgeSuggestions = [{ id: 77, belgeNo: 'ESKI-ONERI', belgeTarihi: '2026-03-01' }];
        // Yeni cari, GUNCEL (yuklenmis) lookup listesinde yer almali - aksi halde stale/bilinmeyen
        // bir secim olarak REDDEDILIR (bkz. gorev 2).
        component.cariKartlar = [ornekCariKart({ id: 5 }), ornekCariKart({ id: 8, unvanAdSoyad: 'Baska Musteri' })];

        component.onCariKartSecildi(ornekCariKart({ id: 8, unvanAdSoyad: 'Baska Musteri' }));

        expect(component.iadeEdilenBelgeSuggestions).toEqual([]);
        expect(component.formData!.cariKartId).toBe(8);
        expect(component.formData!.iadeEdilenBelgeId).toBeNull();
        expect(component.formData!.satirlar).toEqual([]);
        expect(component.kaynakSatirlar).toEqual([]);
        expect(component.kaynakSatirHataMesaji).toBeTruthy();

        const saveEmitSpy = spyOn(component.save, 'emit');
        component.onSaveClick();
        expect(saveEmitSpy).not.toHaveBeenCalled();
    });

    it('ayni cari tekrar secilirse (gercek degisiklik yoksa) mevcut iade referansina dokunulmaz', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi,
            cariKartId: 5,
            iadeEdilenBelgeId: 50,
            satirlar: [ornekIadeSatiri({ kaynakSatirId: '10' })]
        });
        serviceSpy.getKaynakSatirlar.and.returnValue(of([ornekKaynakSatir({ id: 10, iadeEdilebilirKalanMiktar: 6 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        component.cariKartlar = [ornekCariKart({ id: 5 })];

        component.onCariKartSecildi(ornekCariKart({ id: 5 }));

        expect(component.formData!.iadeEdilenBelgeId).toBe(50);
        expect(component.formData!.satirlar!.length).toBe(1);
    });

    it('yon degisince eski cari secenekleri hemen temizlenir ve yukleme sirasinda secim yapilamaz', () => {
        const component = createComponent();
        component.belgeId = 99;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            cariKartId: 5,
            iadeEdilenBelgeId: null,
            satirlar: []
        });
        // Ilk acilis - senkron of([...]) doner, cariKartlar/selectedCari GERCEKTEN dolar.
        serviceSpy.getCariKartLookup.and.returnValue(of([ornekCariKart({ id: 5 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        expect(component.cariKartlar.length).toBe(1);
        expect(component.selectedCari?.id).toBe(5);

        // Yon degisimi baslar - YENI istek HENUZ cozulmeden ESKI liste/secim HEMEN temizlenmis
        // ve yukleniyor bayragi acilmis olmali (bkz. gorev 1).
        const subject = new Subject<TicariBelgeCariKartLookupDto[]>();
        serviceSpy.getCariKartLookup.and.returnValue(subject.asObservable());
        component.onBelgeTipiChange(SatisBelgesiTipi.AlisFaturasi);

        expect(component.cariKartlar).toEqual([]);
        expect(component.filteredCariKartlar).toEqual([]);
        expect(component.selectedCari).toBeNull();
        expect(component.cariKartlarYukleniyor).toBeTrue();

        // Yukleme SURERKEN bir secim denemesi REDDEDILMELI - selectedCari DEGISMEMELI (bkz. gorev 2).
        component.onCariKartSecildi(ornekCariKart({ id: 9, cariTipi: 'Tedarikci', unvanAdSoyad: 'Tedarikci X' }));
        expect(component.selectedCari).toBeNull();
        expect(component.formData!.cariKartId).toBeNull();

        // Yukleme TAMAMLANIR - artik cariKartlarYukleniyor false, ayni secim ARTIK kabul edilmeli.
        subject.next([ornekCariKart({ id: 9, cariTipi: 'Tedarikci', unvanAdSoyad: 'Tedarikci X' })]);
        expect(component.cariKartlarYukleniyor).toBeFalse();

        component.onCariKartSecildi(ornekCariKart({ id: 9, cariTipi: 'Tedarikci', unvanAdSoyad: 'Tedarikci X' }));
        expect(component.formData!.cariKartId).toBe(9);
    });

    it('dialog yeniden acilirken onceki selectedCari gosterilmez', () => {
        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            cariKartId: 5,
            iadeEdilenBelgeId: null,
            satirlar: []
        });
        serviceSpy.getCariKartLookup.and.returnValue(of([ornekCariKart({ id: 5 })]));
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        expect(component.selectedCari?.id).toBe(5);

        // Dialog FARKLI bir belge icin YENIDEN aciliyor - yeni lookup HENUZ cozulmeden onceki
        // belgeye ait eski secim GORUNMEMELI (bkz. gorev 1).
        const subject = new Subject<TicariBelgeCariKartLookupDto[]>();
        serviceSpy.getCariKartLookup.and.returnValue(subject.asObservable());
        component.belgeId = 2;
        component.formData = ornekFormData({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            cariKartId: 8,
            iadeEdilenBelgeId: null,
            satirlar: []
        });
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        expect(component.selectedCari).toBeNull();
        expect(component.cariKartlar).toEqual([]);

        subject.next([ornekCariKart({ id: 8, unvanAdSoyad: 'Yeni Belge Carisi' })]);
        expect(component.selectedCari?.id).toBe(8);
    });

    it('onceki dialogun gec gelen iade-adayi yaniti yeni belgeye uygulanmaz', () => {
        const subject = new Subject<TicariBelgeIadeAdayiDto[]>();
        serviceSpy.getIadeAdaylari.and.returnValue(subject.asObservable());

        const component = createComponent();
        component.belgeId = 1;
        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi, cariKartId: 5, iadeEdilenBelgeId: null });
        component.visible = true;
        component.ngOnChanges(VISIBLE_ILK_ACILIS);

        // Onceki (1.) belge icin bir arama BASLATILIR, yaniti HENUZ gelmez.
        component.searchIadeEdilenBelge({ query: 'ESKI' });

        // Dialog KAPANIP FARKLI bir belge icin YENIDEN aciliyor - bu, onceki aramanin
        // token'ini GECERSIZ kilmali ve onerileri temizlemelidir (bkz. gorev 3).
        component.belgeId = 2;
        component.formData = ornekFormData({ belgeTipi: SatisBelgesiTipi.SatisIadeFaturasi, cariKartId: 9, iadeEdilenBelgeId: null });
        component.ngOnChanges(VISIBLE_ILK_ACILIS);
        expect(component.iadeEdilenBelgeSuggestions).toEqual([]);

        // Onceki (1.) belgenin GEC gelen yaniti simdi gelir - YENI belgeye SESSIZCE uygulanmamali.
        subject.next([{ id: 70, belgeNo: 'ESKI-BELGE-70', belgeTarihi: '2026-01-01' }]);

        expect(component.iadeEdilenBelgeSuggestions).toEqual([]);
    });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { SatisBelgeleriComponent } from './satis-belgeleri.component';
import { SatisBelgesiService } from '../services/satis-belgesi.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { PaketTurleriService } from '../paket-turleri/paket-turleri.service';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { DepolarService } from '../depolar/depolar.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import {
    SatisBelgesiDto,
    SatisBelgesiTipi,
    SatisKaynakModulu,
    TicariBelgeDurumu,
    TicariBelgeFaturalamaDurumu,
    TicariBelgeMuhasebeDurumu
} from '../models/satis-belgesi.model';

/**
 * Bu belgenin işlem kararları (buton görünürlüğü) yalnızca backend'in ürettiği otoriter
 * yetenek alanlarından (guncellenebilirMi vb.) okunmalıdır - legacy `durum` alanı kasıtlı
 * olarak GERÇEK durumla ÇELİŞECEK şekilde ayarlanır, böylece bileşenin bu alanı yeniden
 * yorumlamadığı kanıtlanır.
 */
function ornekBelge(overrides?: Partial<SatisBelgesiDto>): SatisBelgesiDto {
    return {
        id: 1,
        belgeNo: 'ST-1',
        belgeTipi: SatisBelgesiTipi.SatisFaturasi,
        durum: 999 as unknown as SatisBelgesiDto['durum'],
        ticariDurum: TicariBelgeDurumu.Hazir,
        muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onayda,
        faturalamaDurumu: TicariBelgeFaturalamaDurumu.Baslatilmadi,
        kaynakModul: SatisKaynakModulu.Manuel,
        belgeTarihi: '2026-08-01',
        kurumsalMi: false,
        toplamMatrah: 0,
        toplamKdv: 0,
        toplamTevkifatTutari: 0,
        toplamNetKdv: 0,
        genelToplam: 0,
        satirlar: [],
        guncellenebilirMi: false,
        silinebilirMi: false,
        muhasebeOnayinaGonderilebilirMi: false,
        muhasebeOnaylanabilirMi: false,
        reddedilebilirMi: false,
        iptalEdilebilirMi: false,
        muhasebeFisiOlusturulabilirMi: false,
        ...overrides
    };
}

describe('SatisBelgeleriComponent — otoriter yetenek alanlarina gore islem gorunurlugu', () => {
    let fixture: ComponentFixture<SatisBelgeleriComponent>;

    function createComponent(): SatisBelgeleriComponent {
        TestBed.configureTestingModule({
            providers: [
                { provide: SatisBelgesiService, useValue: {} },
                { provide: MuhasebeTesisContextService, useValue: { seciliTesis: () => null } },
                { provide: PaketTurleriService, useValue: {} },
                { provide: CariKartlarService, useValue: {} },
                { provide: DepolarService, useValue: {} },
                { provide: TasinirKartlariService, useValue: {} },
                { provide: KdvIstisnaTanimService, useValue: {} },
                { provide: Router, useValue: {} },
                { provide: ActivatedRoute, useValue: {} }
            ]
        });
        fixture = TestBed.createComponent(SatisBelgeleriComponent);
        return fixture.componentInstance;
    }

    // Bileşen kendi enjektör düzeyinde ConfirmationService/MessageService sağlar (bkz.
    // @Component providers) - bu yüzden gerçek örneğe TestBed.inject DEĞİL, bileşenin kendi
    // debugElement.injector'ından erişilmelidir.
    function messageServiceOf(instance: SatisBelgeleriComponent): MessageService {
        return fixture.debugElement.injector.get(MessageService);
    }

    it('canEdit/canDelete/canGonder/canOnayla/canReddet/canIptal/canFisOlustur dogrudan DTO yetenek alanlarini okur', () => {
        const component = createComponent();

        expect(component.canEdit(ornekBelge({ guncellenebilirMi: true }))).toBe(true);
        expect(component.canEdit(ornekBelge({ guncellenebilirMi: false }))).toBe(false);

        expect(component.canDelete(ornekBelge({ silinebilirMi: true }))).toBe(true);
        expect(component.canDelete(ornekBelge({ silinebilirMi: false }))).toBe(false);

        expect(component.canGonder(ornekBelge({ muhasebeOnayinaGonderilebilirMi: true }))).toBe(true);
        expect(component.canGonder(ornekBelge({ muhasebeOnayinaGonderilebilirMi: false }))).toBe(false);

        expect(component.canOnayla(ornekBelge({ muhasebeOnaylanabilirMi: true }))).toBe(true);
        expect(component.canOnayla(ornekBelge({ muhasebeOnaylanabilirMi: false }))).toBe(false);

        expect(component.canReddet(ornekBelge({ reddedilebilirMi: true }))).toBe(true);
        expect(component.canReddet(ornekBelge({ reddedilebilirMi: false }))).toBe(false);

        expect(component.canIptal(ornekBelge({ iptalEdilebilirMi: true }))).toBe(true);
        expect(component.canIptal(ornekBelge({ iptalEdilebilirMi: false }))).toBe(false);
    });

    it('canFisOlustur yalnizca MuhasebeFisiOlusturulabilirMi true iken true doner - legacy durum alanindan etkilenmez', () => {
        const component = createComponent();

        const destekli = ornekBelge({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onaylandi,
            muhasebeFisId: null,
            muhasebeFisiOlusturulabilirMi: true
        });
        expect(component.canFisOlustur(destekli)).toBe(true);

        const faturaTaslagi = ornekBelge({
            belgeTipi: SatisBelgesiTipi.FaturaTaslagi,
            muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onaylandi,
            muhasebeFisiOlusturulabilirMi: false
        });
        expect(component.canFisOlustur(faturaTaslagi)).toBe(false);

        const proforma = ornekBelge({
            belgeTipi: SatisBelgesiTipi.Proforma,
            muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onaylandi,
            muhasebeFisiOlusturulabilirMi: false
        });
        expect(component.canFisOlustur(proforma)).toBe(false);

        const legacyIade = ornekBelge({
            belgeTipi: SatisBelgesiTipi.IadeFaturasi,
            muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onaylandi,
            muhasebeFisiOlusturulabilirMi: false
        });
        expect(component.canFisOlustur(legacyIade)).toBe(false);

        const fisMevcut = ornekBelge({
            belgeTipi: SatisBelgesiTipi.SatisFaturasi,
            muhasebeDurumu: TicariBelgeMuhasebeDurumu.Onaylandi,
            muhasebeFisId: 42,
            muhasebeFisiOlusturulabilirMi: false
        });
        expect(component.canFisOlustur(fisMevcut)).toBe(false);
    });

    it('muhasebeFisiOlustur canFisOlustur false iken eski "iade henuz desteklenmiyor" yerine genel bir uyari gosterir', () => {
        const component = createComponent();
        const messageService = messageServiceOf(component);
        spyOn(messageService, 'add');

        component.muhasebeFisiOlustur(ornekBelge({ muhasebeFisiOlusturulabilirMi: false }));

        expect(messageService.add).toHaveBeenCalledTimes(1);
        const call = (messageService.add as jasmine.Spy).calls.mostRecent().args[0];
        expect(call.detail).not.toContain('İade');
        expect(call.detail).not.toContain('henüz desteklenmiyor');
    });

    it('openEditDialog guncellenebilirMi false iken dialogu acmaz', () => {
        const component = createComponent();
        const messageService = messageServiceOf(component);
        spyOn(messageService, 'add');

        component.openEditDialog(ornekBelge({ guncellenebilirMi: false }));

        expect(component.dialogVisible()).toBe(false);
        expect(messageService.add).toHaveBeenCalled();
    });
});

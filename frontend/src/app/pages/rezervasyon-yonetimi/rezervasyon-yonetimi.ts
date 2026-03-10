import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, finalize, forkJoin, Observable, switchMap } from 'rxjs';
import { MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import {
    KonaklamaSenaryoDto,
    RezervasyonCheckInKontrolDto,
    RezervasyonDetayDto,
    RezervasyonDegisiklikGecmisiDto,
    RezervasyonIndirimKuraliSecenekDto,
    RezervasyonKaydetRequestDto,
    RezervasyonKayitSonucDto,
    RezervasyonKonaklamaTipiDto,
    RezervasyonKonaklayanKisiDto,
    RezervasyonKonaklayanPlanDto,
    RezervasyonKonaklayanSegmentDto,
    RezervasyonListeDto,
    RezervasyonMisafirTipiDto,
    RezervasyonOdemeOzetDto,
    RezervasyonOdaDegisimKayitDto,
    RezervasyonOdaDegisimSecenekDto,
    RezervasyonOdaTipiDto,
    RezervasyonTesisDto,
    SenaryoFiyatHesaplamaSonucuDto
} from './rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from './rezervasyon-yonetimi.service';

interface DegisiklikPayloadTableColumn {
    field: string;
    header: string;
}

interface DegisiklikPayloadTableData {
    columns: DegisiklikPayloadTableColumn[];
    rows: Record<string, string>[];
}

@Component({
    selector: 'app-rezervasyon-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, InputTextModule, MenuModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './rezervasyon-yonetimi.html',
    providers: [MessageService]
})
export class RezervasyonYonetimi implements OnInit {
    private readonly service = inject(RezervasyonYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    odaTipleri: RezervasyonOdaTipiDto[] = [];
    misafirTipleri: RezervasyonMisafirTipiDto[] = [];
    konaklamaTipleri: RezervasyonKonaklamaTipiDto[] = [];
    senaryolar: KonaklamaSenaryoDto[] = [];
    rezervasyonKayitlari: RezervasyonListeDto[] = [];
    expandedRowKeys: Record<string, boolean> = {};
    rezervasyonDetayById: Record<number, RezervasyonDetayDto> = {};
    detayLoadingByRezervasyonId: Record<number, boolean> = {};
    availableDiscountRules: RezervasyonIndirimKuraliSecenekDto[] = [];
    selectedDiscountRuleIds: number[] = [];
    customDiscountAmount: number | null = null;
    customDiscountDescription = '';
    scenarioPriceBreakdown: SenaryoFiyatHesaplamaSonucuDto | null = null;
    selectedScenarioForDiscount: KonaklamaSenaryoDto | null = null;
    discountDialogVisible = false;
    rezervasyonUcretDetayDialogVisible = false;
    rezervasyonUcretDetayRezervasyonId: number | null = null;
    rezervasyonUcretDetayReferansNo = '';
    degisiklikGecmisiDialogVisible = false;
    degisiklikGecmisiLoading = false;
    degisiklikGecmisiRezervasyonId: number | null = null;
    degisiklikGecmisiReferansNo = '';
    degisiklikGecmisiKayitlari: RezervasyonDegisiklikGecmisiDto[] = [];
    degisiklikPayloadDialogVisible = false;
    degisiklikPayloadDialogTitle = '';
    degisiklikPayloadDialogContent = '';
    degisiklikPayloadDialogMode: 'table' | 'json' = 'table';
    degisiklikPayloadTableColumns: DegisiklikPayloadTableColumn[] = [];
    degisiklikPayloadTableRows: Record<string, string>[] = [];
    konaklayanPlanDialogVisible = false;
    konaklayanPlanLoading = false;
    konaklayanPlanSaving = false;
    konaklayanPlanRezervasyonId: number | null = null;
    konaklayanPlanReferansNo = '';
    konaklayanPlan: RezervasyonKonaklayanPlanDto | null = null;
    odemeDialogVisible = false;
    odemeLoading = false;
    odemeSaving = false;
    odemeRezervasyonId: number | null = null;
    odemeReferansNo = '';
    odemeOzeti: RezervasyonOdemeOzetDto | null = null;
    odemeTutari: number | null = null;
    odemeTipi = 'Nakit';
    odemeAciklama = '';
    odaDegisimDialogVisible = false;
    odaDegisimLoading = false;
    odaDegisimSaving = false;
    odaDegisimRezervasyonId: number | null = null;
    odaDegisimReferansNo = '';
    odaDegisimSecenekleri: RezervasyonOdaDegisimSecenekDto | null = null;
    odaDegisimSecimleri: Record<number, number> = {};
    readonly odemeTipleri = [
        { label: 'Nakit', value: 'Nakit' },
        { label: 'Kredi Karti', value: 'KrediKarti' }
    ];
    rowActionItems: MenuItem[] = [];
    checkActionLoadingByRezervasyonId: Record<number, boolean> = {};

    selectedTesisId: number | null = null;
    selectedOdaTipiId: number | null = null;
    selectedMisafirTipiId: number | null = null;
    selectedKonaklamaTipiId: number | null = null;
    kisiSayisi = 1;
    baslangicTarihi = this.nowInput();
    bitisTarihi = this.tomorrowInput();
    misafirAdiSoyadi = '';
    misafirTelefon = '';
    misafirEposta = '';
    tcKimlikNo = '';
    pasaportNo = '';
    notlar = '';

    loadingReferences = false;
    loadingResults = false;
    loadingRezervasyonlar = false;
    loadingDiscountRules = false;
    calculatingScenarioPrice = false;
    saving = false;
    private readonly defaultGirisSaati = '14:00';
    private readonly defaultCikisSaati = '10:00';
    private readonly durumTaslak = 'Taslak';
    private readonly durumOnayli = 'Onayli';
    private readonly durumCheckInTamamlandi = 'CheckInTamamlandi';
    private readonly durumCheckOutTamamlandi = 'CheckOutTamamlandi';
    private readonly durumIptal = 'Iptal';

    get canView(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.Manage');
    }

    get canApplyCustomDiscount(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.CustomIndirimGirebilir');
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        this.loadReferences();
    }

    onRowExpand(event: { data?: RezervasyonListeDto }): void {
        const rezervasyonId = event.data?.id;
        if (!rezervasyonId || rezervasyonId <= 0) {
            return;
        }

        this.loadRezervasyonDetay(rezervasyonId);
    }

    onTesisChange(): void {
        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.odaTipleri = [];
            this.selectedOdaTipiId = null;
            this.senaryolar = [];
            this.rezervasyonKayitlari = [];
            this.availableDiscountRules = [];
            return;
        }

        this.applySelectedTesisDateTimes();
        this.loadOdaTipleri(this.selectedTesisId, true);
        this.loadRezervasyonKayitlari(this.selectedTesisId);
    }

    search(): void {
        if (!this.canView) {
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Lutfen bir tesis seciniz.' });
            return;
        }

        if (!this.selectedMisafirTipiId || this.selectedMisafirTipiId <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Lutfen bir misafir tipi seciniz.' });
            return;
        }

        if (!this.selectedKonaklamaTipiId || this.selectedKonaklamaTipiId <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Lutfen bir konaklama tipi seciniz.' });
            return;
        }

        if (this.kisiSayisi <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Kisi sayisi sifirdan buyuk olmalidir.' });
            return;
        }

        if (!this.baslangicTarihi || !this.bitisTarihi) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Baslangic ve bitis tarihi zorunludur.' });
            return;
        }

        if (new Date(this.baslangicTarihi).getTime() >= new Date(this.bitisTarihi).getTime()) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Baslangic tarihi bitis tarihinden kucuk olmalidir.' });
            return;
        }

        this.loadingResults = true;
        this.service
            .searchKonaklamaSenaryolari({
                tesisId: this.selectedTesisId,
                odaTipiId: this.selectedOdaTipiId,
                misafirTipiId: this.selectedMisafirTipiId,
                konaklamaTipiId: this.selectedKonaklamaTipiId,
                kisiSayisi: this.kisiSayisi,
                baslangicTarihi: this.toIsoDate(this.baslangicTarihi),
                bitisTarihi: this.toIsoDate(this.bitisTarihi)
            })
            .pipe(
                finalize(() => {
                    this.loadingResults = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.senaryolar = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.senaryolar = [];
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    reserveScenario(scenario: KonaklamaSenaryoDto): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Tesis secimi zorunludur.' });
            return;
        }

        if (!this.misafirAdiSoyadi.trim()) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Misafir adi soyadi zorunludur.' });
            return;
        }

        if (!this.misafirTelefon.trim()) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Misafir telefonu zorunludur.' });
            return;
        }

        const payload: RezervasyonKaydetRequestDto = {
            tesisId: this.selectedTesisId,
            kisiSayisi: this.kisiSayisi,
            misafirTipiId: this.selectedMisafirTipiId!,
            konaklamaTipiId: this.selectedKonaklamaTipiId!,
            girisTarihi: this.toIsoDate(this.baslangicTarihi),
            cikisTarihi: this.toIsoDate(this.bitisTarihi),
            misafirAdiSoyadi: this.misafirAdiSoyadi.trim(),
            misafirTelefon: this.misafirTelefon.trim(),
            misafirEposta: this.normalizeOptional(this.misafirEposta),
            tcKimlikNo: this.normalizeOptional(this.tcKimlikNo),
            pasaportNo: this.normalizeOptional(this.pasaportNo),
            notlar: this.normalizeOptional(this.notlar),
            toplamBazUcret: Number.isFinite(scenario.toplamBazUcret) ? scenario.toplamBazUcret : scenario.toplamNihaiUcret,
            toplamUcret: Number.isFinite(scenario.toplamNihaiUcret) ? scenario.toplamNihaiUcret : 0,
            paraBirimi: (scenario.paraBirimi || 'TRY').toUpperCase(),
            uygulananIndirimler: [...(scenario.uygulananIndirimler ?? [])],
            segmentler: scenario.segmentler.map((segment) => ({
                baslangicTarihi: segment.baslangicTarihi,
                bitisTarihi: segment.bitisTarihi,
                odaAtamalari: segment.odaAtamalari.map((assignment) => ({
                    odaId: assignment.odaId,
                    ayrilanKisiSayisi: assignment.ayrilanKisiSayisi
                }))
            }))
        };

        this.saving = true;
        this.service
            .createRezervasyon(payload)
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: `Rezervasyon kaydedildi. Referans: ${result.referansNo}`
                    });
                    this.loadRezervasyonKayitlari(this.selectedTesisId);
                    this.search();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    openDiscountDialog(scenario: KonaklamaSenaryoDto): void {
        if (!this.selectedTesisId || !this.selectedMisafirTipiId || !this.selectedKonaklamaTipiId) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Tesis, misafir tipi ve konaklama tipi secimi zorunludur.' });
            return;
        }

        const mevcutIndirimler = [...(scenario.uygulananIndirimler ?? [])];
        this.selectedScenarioForDiscount = scenario;
        this.selectedDiscountRuleIds = [];
        this.customDiscountAmount = null;
        this.customDiscountDescription = '';
        this.scenarioPriceBreakdown = {
            toplamBazUcret: scenario.toplamBazUcret,
            toplamNihaiUcret: scenario.toplamNihaiUcret,
            paraBirimi: scenario.paraBirimi || 'TRY',
            uygulananIndirimler: mevcutIndirimler
        };
        this.discountDialogVisible = true;

        this.loadingDiscountRules = true;
        this.service
            .getIndirimKurallari(
                this.selectedTesisId,
                this.selectedMisafirTipiId,
                this.selectedKonaklamaTipiId,
                this.toIsoDate(this.baslangicTarihi),
                this.toIsoDate(this.bitisTarihi)
            )
            .pipe(
                finalize(() => {
                    this.loadingDiscountRules = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (rules) => {
                    this.availableDiscountRules = rules;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.availableDiscountRules = [];
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    hesaplaSenaryoUcreti(): void {
        if (!this.selectedScenarioForDiscount || this.calculatingScenarioPrice) {
            return;
        }

        if ((this.customDiscountAmount ?? 0) < 0) {
            this.messageService.add({ severity: 'warn', summary: 'Gecersiz Tutar', detail: 'Custom indirim tutari sifirdan kucuk olamaz.' });
            return;
        }

        if (!this.selectedTesisId || !this.selectedMisafirTipiId || !this.selectedKonaklamaTipiId) {
            return;
        }

        this.calculatingScenarioPrice = true;
        this.service
            .hesaplaSenaryoFiyati({
                tesisId: this.selectedTesisId,
                misafirTipiId: this.selectedMisafirTipiId,
                konaklamaTipiId: this.selectedKonaklamaTipiId,
                baslangicTarihi: this.toIsoDate(this.baslangicTarihi),
                bitisTarihi: this.toIsoDate(this.bitisTarihi),
                segmentler: this.selectedScenarioForDiscount.segmentler.map((segment) => ({
                    baslangicTarihi: segment.baslangicTarihi,
                    bitisTarihi: segment.bitisTarihi,
                    odaAtamalari: segment.odaAtamalari.map((atama) => ({
                        odaId: atama.odaId,
                        ayrilanKisiSayisi: atama.ayrilanKisiSayisi
                    }))
                })),
                seciliIndirimKuraliIds: this.selectedDiscountRuleIds
            })
            .pipe(
                finalize(() => {
                    this.calculatingScenarioPrice = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    const withCustomDiscount = this.applyCustomDiscountIfNeeded(result);
                    this.scenarioPriceBreakdown = withCustomDiscount;
                    this.selectedScenarioForDiscount!.toplamBazUcret = withCustomDiscount.toplamBazUcret;
                    this.selectedScenarioForDiscount!.toplamNihaiUcret = withCustomDiscount.toplamNihaiUcret;
                    this.selectedScenarioForDiscount!.paraBirimi = withCustomDiscount.paraBirimi;
                    this.selectedScenarioForDiscount!.uygulananIndirimler = [...withCustomDiscount.uygulananIndirimler];
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    closeDiscountDialog(): void {
        this.discountDialogVisible = false;
        this.selectedScenarioForDiscount = null;
        this.availableDiscountRules = [];
        this.selectedDiscountRuleIds = [];
        this.customDiscountAmount = null;
        this.customDiscountDescription = '';
        this.scenarioPriceBreakdown = null;
    }

    private applyCustomDiscountIfNeeded(result: SenaryoFiyatHesaplamaSonucuDto): SenaryoFiyatHesaplamaSonucuDto {
        if (!this.canApplyCustomDiscount) {
            return result;
        }

        const customAmount = Number(this.customDiscountAmount ?? 0);
        if (!Number.isFinite(customAmount) || customAmount <= 0) {
            return result;
        }

        const discountAmount = Math.min(result.toplamNihaiUcret, customAmount);
        if (discountAmount <= 0) {
            return result;
        }

        const afterDiscount = result.toplamNihaiUcret - discountAmount;
        const customRuleName = this.customDiscountDescription.trim().length > 0
            ? this.customDiscountDescription.trim()
            : 'Custom Indirim';

        return {
            ...result,
            toplamNihaiUcret: afterDiscount,
            uygulananIndirimler: [
                ...result.uygulananIndirimler,
                {
                    indirimKuraliId: 0,
                    kuralAdi: customRuleName,
                    indirimTutari: discountAmount,
                    sonrasiTutar: afterDiscount
                }
            ]
        };
    }

    openRezervasyonUcretDetay(kayit: RezervasyonListeDto): void {
        if (!kayit?.id || kayit.id <= 0) {
            return;
        }

        this.rezervasyonUcretDetayRezervasyonId = kayit.id;
        this.rezervasyonUcretDetayReferansNo = kayit.referansNo;
        this.rezervasyonUcretDetayDialogVisible = true;
        this.loadRezervasyonDetay(kayit.id);
    }

    closeRezervasyonUcretDetayDialog(): void {
        this.rezervasyonUcretDetayDialogVisible = false;
        this.rezervasyonUcretDetayRezervasyonId = null;
        this.rezervasyonUcretDetayReferansNo = '';
    }

    getSelectedRezervasyonUcretDetay(): RezervasyonDetayDto | null {
        return this.getRezervasyonDetay(this.rezervasyonUcretDetayRezervasyonId);
    }

    openDegisiklikGecmisiDialog(kayit: RezervasyonListeDto): void {
        if (!this.canView || !kayit?.id || kayit.id <= 0) {
            return;
        }

        this.degisiklikGecmisiDialogVisible = true;
        this.degisiklikGecmisiRezervasyonId = kayit.id;
        this.degisiklikGecmisiReferansNo = kayit.referansNo;
        this.degisiklikGecmisiKayitlari = [];
        this.loadDegisiklikGecmisi(kayit.id);
    }

    closeDegisiklikGecmisiDialog(): void {
        this.degisiklikGecmisiDialogVisible = false;
        this.degisiklikGecmisiLoading = false;
        this.degisiklikGecmisiRezervasyonId = null;
        this.degisiklikGecmisiReferansNo = '';
        this.degisiklikGecmisiKayitlari = [];
        this.closeDegisiklikPayloadDialog();
    }

    getGecmisIslemLabel(islemTipi: string): string {
        switch (islemTipi) {
            case 'RezervasyonOlusturuldu':
                return 'Rezervasyon Oluşturuldu';
            case 'KonaklayanPlaniKaydedildi':
                return 'Konaklayan Planı Kaydedildi';
            case 'OdaDegisimiYapildi':
                return 'Oda Değişimi Yapıldı';
            case 'CheckInTamamlandi':
                return 'Check-in Tamamlandı';
            case 'CheckOutTamamlandi':
                return 'Check-out Tamamlandı';
            case 'IptalEdildi':
                return 'Rezervasyon İptal Edildi';
            case 'IptalGeriAlindi':
                return 'İptal Geri Alındı';
            case 'OdemeKaydedildi':
                return 'Ödeme Kaydedildi';
            default:
                return islemTipi;
        }
    }

    formatJsonForDisplay(value: string | null | undefined): string {
        if (!value || value.trim().length === 0) {
            return '-';
        }

        try {
            return JSON.stringify(JSON.parse(value), null, 2);
        } catch {
            return value;
        }
    }

    hasJsonPayload(value: string | null | undefined): boolean {
        return !!value && value.trim().length > 0 && value.trim() !== '[]' && value.trim() !== '{}';
    }

    openDegisiklikPayloadDialog(kayit: RezervasyonDegisiklikGecmisiDto, payloadType: 'onceki' | 'yeni'): void {
        const payload = payloadType === 'onceki' ? kayit.oncekiDegerJson : kayit.yeniDegerJson;
        if (!this.hasJsonPayload(payload)) {
            return;
        }
        const payloadValue = payload ?? '';

        this.degisiklikPayloadDialogTitle = `${this.getGecmisIslemLabel(kayit.islemTipi)} - ${payloadType === 'onceki' ? 'Onceki Deger' : 'Yeni Deger'}`;
        const parsedPayload = this.tryParseJson(payloadValue);
        const tableData = this.tryBuildKonaklayanPlaniTableData(parsedPayload) ?? this.tryBuildGenericTableData(parsedPayload);

        if (tableData) {
            this.degisiklikPayloadDialogMode = 'table';
            this.degisiklikPayloadTableColumns = tableData.columns;
            this.degisiklikPayloadTableRows = tableData.rows;
            this.degisiklikPayloadDialogContent = '';
        } else {
            this.degisiklikPayloadDialogMode = 'json';
            this.degisiklikPayloadTableColumns = [];
            this.degisiklikPayloadTableRows = [];
            this.degisiklikPayloadDialogContent = this.formatJsonForDisplay(payloadValue);
        }
        this.degisiklikPayloadDialogVisible = true;
    }

    closeDegisiklikPayloadDialog(): void {
        this.degisiklikPayloadDialogVisible = false;
        this.degisiklikPayloadDialogTitle = '';
        this.degisiklikPayloadDialogContent = '';
        this.degisiklikPayloadDialogMode = 'table';
        this.degisiklikPayloadTableColumns = [];
        this.degisiklikPayloadTableRows = [];
    }

    getDegisiklikOzet(kayit: RezervasyonDegisiklikGecmisiDto): string {
        const oncekiVar = this.hasJsonPayload(kayit.oncekiDegerJson);
        const yeniVar = this.hasJsonPayload(kayit.yeniDegerJson);

        if (!oncekiVar && !yeniVar) {
            return 'Detay yok';
        }

        if (oncekiVar && yeniVar) {
            return 'Onceki + Yeni';
        }

        return oncekiVar ? 'Sadece Onceki' : 'Sadece Yeni';
    }

    private tryParseJson(value: string): unknown {
        try {
            return JSON.parse(value);
        } catch {
            return null;
        }
    }

    private tryBuildKonaklayanPlaniTableData(payload: unknown): DegisiklikPayloadTableData | null {
        if (!Array.isArray(payload) || payload.length === 0) {
            return null;
        }

        const items = payload.filter((x): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x));
        if (items.length !== payload.length) {
            return null;
        }

        const rows: Record<string, string>[] = [];
        for (const item of items) {
            const siraNo = this.readUnknown(item, ['SiraNo', 'siraNo']);
            const adSoyad = this.readUnknown(item, ['AdSoyad', 'adSoyad']);
            const tcKimlikNo = this.readUnknown(item, ['TcKimlikNo', 'tcKimlikNo']);
            const pasaportNo = this.readUnknown(item, ['PasaportNo', 'pasaportNo']);
            const atamalarUnknown = this.readUnknown(item, ['Atamalar', 'atamalar']);

            if (typeof siraNo === 'undefined' || typeof adSoyad === 'undefined' || !Array.isArray(atamalarUnknown)) {
                return null;
            }

            const atamaOzetleri = atamalarUnknown
                .filter((a): a is Record<string, unknown> => !!a && typeof a === 'object' && !Array.isArray(a))
                .map((atama) => {
                    const segmentId = this.readUnknown(atama, ['RezervasyonSegmentId', 'rezervasyonSegmentId', 'SegmentId', 'segmentId']);
                    const odaId = this.readUnknown(atama, ['OdaId', 'odaId']);
                    return `Segment ${this.toDisplayString(segmentId)} -> Oda ${this.toDisplayString(odaId)}`;
                });

            rows.push({
                siraNo: this.toDisplayString(siraNo),
                adSoyad: this.toDisplayString(adSoyad),
                tcKimlikNo: this.toDisplayString(tcKimlikNo),
                pasaportNo: this.toDisplayString(pasaportNo),
                atamalar: atamaOzetleri.length > 0 ? atamaOzetleri.join('\n') : '-'
            });
        }

        return {
            columns: [
                { field: 'siraNo', header: 'Sıra' },
                { field: 'adSoyad', header: 'Ad Soyad' },
                { field: 'tcKimlikNo', header: 'TC Kimlik No' },
                { field: 'pasaportNo', header: 'Pasaport No' },
                { field: 'atamalar', header: 'Atamalar' }
            ],
            rows
        };
    }

    private tryBuildGenericTableData(payload: unknown): DegisiklikPayloadTableData | null {
        if (Array.isArray(payload)) {
            if (payload.length === 0) {
                return {
                    columns: [{ field: 'bilgi', header: 'Bilgi' }],
                    rows: [{ bilgi: 'Kayıt yok' }]
                };
            }

            const objectItems = payload.filter((x): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x));
            if (objectItems.length === payload.length) {
                const keys = Array.from(
                    new Set(objectItems.flatMap((x) => Object.keys(x)))
                );

                return {
                    columns: keys.map((key) => ({ field: key, header: this.toDisplayHeader(key) })),
                    rows: objectItems.map((item) => {
                        const row: Record<string, string> = {};
                        for (const key of keys) {
                            row[key] = this.toDisplayString(item[key]);
                        }
                        return row;
                    })
                };
            }

            return {
                columns: [{ field: 'deger', header: 'Deger' }],
                rows: payload.map((x) => ({ deger: this.toDisplayString(x) }))
            };
        }

        if (payload && typeof payload === 'object') {
            const obj = payload as Record<string, unknown>;
            const rows = Object.keys(obj).map((key) => ({
                alan: this.toDisplayHeader(key),
                deger: this.toDisplayString(obj[key])
            }));

            return {
                columns: [
                    { field: 'alan', header: 'Alan' },
                    { field: 'deger', header: 'Deger' }
                ],
                rows
            };
        }

        if (typeof payload !== 'undefined' && payload !== null) {
            return {
                columns: [{ field: 'deger', header: 'Deger' }],
                rows: [{ deger: this.toDisplayString(payload) }]
            };
        }

        return null;
    }

    private readUnknown(source: Record<string, unknown>, keys: string[]): unknown {
        for (const key of keys) {
            if (Object.prototype.hasOwnProperty.call(source, key)) {
                return source[key];
            }
        }

        return undefined;
    }

    private toDisplayHeader(value: string): string {
        return value
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/_/g, ' ')
            .replace(/^\w/, (x) => x.toUpperCase());
    }

    private toDisplayString(value: unknown): string {
        if (value === null || typeof value === 'undefined') {
            return '-';
        }

        if (typeof value === 'string') {
            return value.trim().length > 0 ? value : '-';
        }

        if (typeof value === 'number' || typeof value === 'boolean') {
            return String(value);
        }

        if (Array.isArray(value)) {
            if (value.length === 0) {
                return '-';
            }

            const primitiveArray = value.every((x) => x === null || ['string', 'number', 'boolean'].includes(typeof x));
            if (primitiveArray) {
                return value.map((x) => this.toDisplayString(x)).join(', ');
            }

            return `${value.length} kayıt`;
        }

        if (typeof value === 'object') {
            return JSON.stringify(value);
        }

        return String(value);
    }

    openKonaklayanPlaniDialog(kayit: RezervasyonListeDto): void {
        if (!this.canManage || !kayit?.id || kayit.id <= 0) {
            return;
        }

        this.konaklayanPlanDialogVisible = true;
        this.konaklayanPlanRezervasyonId = kayit.id;
        this.konaklayanPlanReferansNo = kayit.referansNo;
        this.konaklayanPlan = null;
        this.loadKonaklayanPlani(kayit.id);
    }

    closeKonaklayanPlaniDialog(): void {
        this.konaklayanPlanDialogVisible = false;
        this.konaklayanPlanRezervasyonId = null;
        this.konaklayanPlanReferansNo = '';
        this.konaklayanPlan = null;
        this.konaklayanPlanLoading = false;
        this.konaklayanPlanSaving = false;
    }

    kaydetKonaklayanPlani(): void {
        if (!this.canManage || !this.konaklayanPlanRezervasyonId || !this.konaklayanPlan) {
            return;
        }

        this.konaklayanPlanSaving = true;
        this.service
            .saveKonaklayanPlani(this.konaklayanPlanRezervasyonId, {
                konaklayanlar: this.konaklayanPlan.konaklayanlar.map((kisi) => ({
                    siraNo: kisi.siraNo,
                    adSoyad: (kisi.adSoyad ?? '').trim(),
                    tcKimlikNo: this.normalizeOptional(kisi.tcKimlikNo ?? ''),
                    pasaportNo: this.normalizeOptional(kisi.pasaportNo ?? ''),
                    atamalar: kisi.atamalar.map((atama) => ({
                        segmentId: atama.segmentId,
                        odaId: atama.odaId
                    }))
                }))
            })
            .pipe(
                finalize(() => {
                    this.konaklayanPlanSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (plan) => {
                    this.konaklayanPlan = plan;
                    const rezervasyonId = this.konaklayanPlanRezervasyonId;
                    if (rezervasyonId && rezervasyonId > 0) {
                        const kayit = this.rezervasyonKayitlari.find((x) => x.id === rezervasyonId);
                        if (kayit) {
                            kayit.konaklayanPlaniTamamlandi = this.isKonaklayanPlanComplete(plan);
                        }
                    }
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Konaklayan plani kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private isKonaklayanPlanComplete(plan: RezervasyonKonaklayanPlanDto): boolean {
        if (!plan || plan.segmentler.length === 0 || plan.konaklayanlar.length !== plan.kisiSayisi) {
            return false;
        }

        const segmentIds = new Set(plan.segmentler.map((x) => x.segmentId));
        return plan.konaklayanlar.every((kisi) =>
            (kisi.adSoyad ?? '').trim().length > 0
            && kisi.atamalar.length === segmentIds.size
            && kisi.atamalar.every((atama) => segmentIds.has(atama.segmentId) && (atama.odaId ?? 0) > 0));
    }

    canCompleteCheckIn(kayit: RezervasyonListeDto): boolean {
        if (!this.canManage) {
            return false;
        }

        return kayit.konaklayanPlaniTamamlandi
            && !kayit.odaDegisimiGerekli
            && (kayit.rezervasyonDurumu === this.durumTaslak || kayit.rezervasyonDurumu === this.durumOnayli);
    }

    canCompleteCheckOut(kayit: RezervasyonListeDto): boolean {
        return this.canManage
            && kayit.rezervasyonDurumu === this.durumCheckInTamamlandi
            && (kayit.kalanTutar ?? 0) <= 0;
    }

    isCheckActionLoading(rezervasyonId: number): boolean {
        return this.checkActionLoadingByRezervasyonId[rezervasyonId] ?? false;
    }

    canCancelReservation(kayit: RezervasyonListeDto): boolean {
        if (!this.canManage) {
            return false;
        }

        return kayit.rezervasyonDurumu !== this.durumCheckOutTamamlandi;
    }

    canOpenPaymentDialog(kayit: RezervasyonListeDto): boolean {
        if (!this.canManage) {
            return false;
        }

        return kayit.rezervasyonDurumu !== this.durumIptal;
    }

    canOpenOdaDegisimDialog(kayit: RezervasyonListeDto): boolean {
        if (!this.canManage) {
            return false;
        }

        return kayit.odaDegisimiGerekli
            && (kayit.rezervasyonDurumu === this.durumTaslak || kayit.rezervasyonDurumu === this.durumOnayli);
    }

    hasAnyRowAction(kayit: RezervasyonListeDto): boolean {
        return this.getRowActions(kayit).length > 0;
    }

    getRowActions(kayit: RezervasyonListeDto): MenuItem[] {
        const items: MenuItem[] = [];

        if (this.canView) {
            items.push({
                label: 'Gecmis',
                icon: 'pi pi-history',
                command: () => this.openDegisiklikGecmisiDialog(kayit)
            });
        }

        if (!this.canManage) {
            return items;
        }

        items.push({
            label: 'Plan',
            icon: 'pi pi-users',
            command: () => this.openKonaklayanPlaniDialog(kayit)
        });

        if (this.canOpenOdaDegisimDialog(kayit)) {
            items.push({
                label: 'Oda Degistir',
                icon: 'pi pi-sync',
                command: () => this.openOdaDegisimDialog(kayit)
            });
        }

        if (this.canCompleteCheckIn(kayit)) {
            items.push({
                label: 'Check-in',
                icon: 'pi pi-arrow-right',
                command: () => this.tamamlaCheckIn(kayit)
            });
        }

        if (this.canCompleteCheckOut(kayit)) {
            items.push({
                label: 'Check-out',
                icon: 'pi pi-arrow-left',
                command: () => this.tamamlaCheckOut(kayit)
            });
        }

        if (this.canOpenPaymentDialog(kayit)) {
            items.push({
                label: 'Odeme',
                icon: 'pi pi-credit-card',
                command: () => this.openOdemeDialog(kayit)
            });
        }

        if (this.canCancelReservation(kayit)) {
            items.push({
                label: kayit.rezervasyonDurumu === this.durumIptal ? 'Iptali Geri Al' : 'Iptal Et',
                icon: 'pi pi-times',
                command: () => this.iptalEt(kayit)
            });
        }

        return items;
    }

    openRowActionsMenu(menu: { toggle: (event: Event) => void }, event: Event, kayit: RezervasyonListeDto): void {
        if (this.isCheckActionLoading(kayit.id)) {
            return;
        }

        this.rowActionItems = this.getRowActions(kayit);
        menu.toggle(event);
    }

    tamamlaCheckIn(kayit: RezervasyonListeDto): void {
        if (!this.canCompleteCheckIn(kayit) || this.isCheckActionLoading(kayit.id)) {
            return;
        }

        this.checkActionLoadingByRezervasyonId[kayit.id] = true;
        this.service
            .getCheckInKontrol(kayit.id)
            .pipe(
                switchMap((kontrol) => this.executeCheckInIfAllowed(kayit, kontrol)),
                finalize(() => {
                    this.checkActionLoadingByRezervasyonId[kayit.id] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.updateRezervasyonDurumu(kayit.id, result.rezervasyonDurumu);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: `Check-in tamamlandi. Referans: ${result.referansNo}`
                    });
                    this.openOdemeDialog(kayit);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private executeCheckInIfAllowed(kayit: RezervasyonListeDto, kontrol: RezervasyonCheckInKontrolDto): Observable<RezervasyonKayitSonucDto> {
        if (kontrol.uyarilar.length > 0) {
            const warningDetail = kontrol.uyarilar
                .map((x) => `${x.odaNo} - ${x.binaAdi} (${x.temizlikDurumu})`)
                .join(', ');

            this.messageService.add({
                severity: kontrol.checkInYapilabilir ? 'warn' : 'error',
                summary: kontrol.checkInYapilabilir ? 'Uyari' : 'Check-in Engellendi',
                detail: kontrol.checkInYapilabilir
                    ? `Oda durumu uyarisi: ${warningDetail}`
                    : `Hazir olmayan odalar var: ${warningDetail}`
            });
        }

        if (!kontrol.checkInYapilabilir) {
            return EMPTY as Observable<RezervasyonKayitSonucDto>;
        }

        return this.service.tamamlaCheckIn(kayit.id);
    }

    iptalEt(kayit: RezervasyonListeDto): void {
        if (!this.canCancelReservation(kayit) || this.isCheckActionLoading(kayit.id)) {
            return;
        }

        const isRevert = kayit.rezervasyonDurumu === this.durumIptal;
        const confirmationMessage = isRevert
            ? 'Bu rezervasyonun iptalini geri alip Taslak durumuna donmek istediginize emin misiniz?'
            : 'Bu rezervasyonu iptal etmek istediginize emin misiniz?';

        if (!window.confirm(confirmationMessage)) {
            return;
        }

        this.checkActionLoadingByRezervasyonId[kayit.id] = true;
        this.service
            .iptalEt(kayit.id)
            .pipe(
                finalize(() => {
                    this.checkActionLoadingByRezervasyonId[kayit.id] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.updateRezervasyonDurumu(kayit.id, result.rezervasyonDurumu);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: result.rezervasyonDurumu === this.durumIptal
                            ? `Rezervasyon iptal edildi. Referans: ${result.referansNo}`
                            : `Rezervasyon iptali geri alindi. Referans: ${result.referansNo}`
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    openOdaDegisimDialog(kayit: RezervasyonListeDto): void {
        if (!this.canOpenOdaDegisimDialog(kayit) || this.odaDegisimLoading) {
            return;
        }

        this.odaDegisimDialogVisible = true;
        this.odaDegisimRezervasyonId = kayit.id;
        this.odaDegisimReferansNo = kayit.referansNo;
        this.odaDegisimSecenekleri = null;
        this.odaDegisimSecimleri = {};
        this.odaDegisimLoading = true;

        this.service
            .getOdaDegisimSecenekleri(kayit.id)
            .pipe(
                finalize(() => {
                    this.odaDegisimLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odaDegisimSecenekleri = result;
                    this.odaDegisimSecimleri = {};
                    for (const kayitSecenegi of result.kayitlar) {
                        const firstCandidate = kayitSecenegi.adayOdalar[0];
                        if (firstCandidate && firstCandidate.odaId > 0) {
                            this.odaDegisimSecimleri[kayitSecenegi.rezervasyonSegmentOdaAtamaId] = firstCandidate.odaId;
                        }
                    }
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.odaDegisimSecenekleri = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    closeOdaDegisimDialog(): void {
        this.odaDegisimDialogVisible = false;
        this.odaDegisimLoading = false;
        this.odaDegisimSaving = false;
        this.odaDegisimRezervasyonId = null;
        this.odaDegisimReferansNo = '';
        this.odaDegisimSecenekleri = null;
        this.odaDegisimSecimleri = {};
    }

    kaydetOdaDegisimi(): void {
        if (!this.odaDegisimRezervasyonId || !this.odaDegisimSecenekleri || !this.canKaydetOdaDegisimi()) {
            return;
        }

        this.odaDegisimSaving = true;
        this.service
            .saveOdaDegisimi(this.odaDegisimRezervasyonId, {
                atamalar: this.odaDegisimSecenekleri.kayitlar.map((item) => ({
                    rezervasyonSegmentOdaAtamaId: item.rezervasyonSegmentOdaAtamaId,
                    yeniOdaId: this.getOdaDegisimSeciliOdaId(item)
                }))
            })
            .pipe(
                finalize(() => {
                    this.odaDegisimSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    const kayit = this.rezervasyonKayitlari.find((x) => x.id === result.id);
                    if (kayit) {
                        kayit.odaDegisimiGerekli = false;
                    }

                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: `Oda degisimi kaydedildi. Referans: ${result.referansNo}`
                    });

                    const selectedTesis = this.selectedTesisId && this.selectedTesisId > 0 ? this.selectedTesisId : null;
                    this.loadRezervasyonKayitlari(selectedTesis);
                    this.closeOdaDegisimDialog();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getOdaDegisimAdayOptions(kayit: RezervasyonOdaDegisimKayitDto): { label: string; value: number }[] {
        return kayit.adayOdalar.map((aday) => ({
            value: aday.odaId,
            label: `${aday.odaNo} - ${aday.binaAdi} (${aday.odaTipiAdi}, kalan ${aday.kalanKapasite})`
        }));
    }

    getOdaDegisimSeciliOdaId(kayit: RezervasyonOdaDegisimKayitDto): number {
        return this.odaDegisimSecimleri[kayit.rezervasyonSegmentOdaAtamaId] ?? 0;
    }

    setOdaDegisimSeciliOdaId(kayit: RezervasyonOdaDegisimKayitDto, odaId: number): void {
        this.odaDegisimSecimleri[kayit.rezervasyonSegmentOdaAtamaId] = odaId;
    }

    canKaydetOdaDegisimi(): boolean {
        if (this.odaDegisimLoading || this.odaDegisimSaving || !this.odaDegisimSecenekleri) {
            return false;
        }

        if (this.odaDegisimSecenekleri.kayitlar.length === 0) {
            return false;
        }

        return this.odaDegisimSecenekleri.kayitlar.every((kayit) =>
            kayit.adayOdalar.length > 0 && this.getOdaDegisimSeciliOdaId(kayit) > 0);
    }

    openOdemeDialog(kayit: RezervasyonListeDto): void {
        if (!this.canOpenPaymentDialog(kayit)) {
            return;
        }

        this.odemeDialogVisible = true;
        this.odemeRezervasyonId = kayit.id;
        this.odemeReferansNo = kayit.referansNo;
        this.odemeOzeti = null;
        this.odemeTutari = null;
        this.odemeTipi = 'Nakit';
        this.odemeAciklama = '';
        this.loadOdemeOzeti(kayit.id);
    }

    closeOdemeDialog(): void {
        this.odemeDialogVisible = false;
        this.odemeLoading = false;
        this.odemeSaving = false;
        this.odemeRezervasyonId = null;
        this.odemeReferansNo = '';
        this.odemeOzeti = null;
        this.odemeTutari = null;
        this.odemeTipi = 'Nakit';
        this.odemeAciklama = '';
    }

    kaydetOdeme(): void {
        if (!this.odemeRezervasyonId || !this.odemeOzeti || this.odemeSaving) {
            return;
        }

        const tutar = Number(this.odemeTutari ?? 0);
        if (!Number.isFinite(tutar) || tutar <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Gecersiz Tutar', detail: 'Odeme tutari sifirdan buyuk olmalidir.' });
            return;
        }

        this.odemeSaving = true;
        this.service
            .kaydetOdeme(this.odemeRezervasyonId, {
                odemeTutari: tutar,
                odemeTipi: this.odemeTipi,
                aciklama: this.normalizeOptional(this.odemeAciklama)
            })
            .pipe(
                finalize(() => {
                    this.odemeSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.odemeTutari = result.kalanTutar > 0 ? result.kalanTutar : null;
                    this.odemeAciklama = '';
                    const kayit = this.rezervasyonKayitlari.find((x) => x.id === result.rezervasyonId);
                    if (kayit) {
                        kayit.odenenTutar = result.odenenTutar;
                        kayit.kalanTutar = result.kalanTutar;
                    }
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Odeme kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    tamamlaCheckOut(kayit: RezervasyonListeDto): void {
        if (!this.canCompleteCheckOut(kayit) || this.isCheckActionLoading(kayit.id)) {
            return;
        }

        this.checkActionLoadingByRezervasyonId[kayit.id] = true;
        this.service
            .tamamlaCheckOut(kayit.id)
            .pipe(
                finalize(() => {
                    this.checkActionLoadingByRezervasyonId[kayit.id] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.updateRezervasyonDurumu(kayit.id, result.rezervasyonDurumu);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: `Check-out tamamlandi. Referans: ${result.referansNo}`
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getKonaklayanSegmentler(): RezervasyonKonaklayanSegmentDto[] {
        return this.konaklayanPlan?.segmentler ?? [];
    }

    getKonaklayanOdaSecenekleri(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): { label: string; value: number }[] {
        const segment = this.konaklayanPlan?.segmentler.find((x) => x.segmentId === segmentId);
        if (!segment) {
            return [];
        }

        const currentSelection = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        return segment.odaSecenekleri
            .filter((oda) => {
                const selectedCount = this.getSegmentOdaSelectedCount(segmentId, oda.odaId);
                if (currentSelection === oda.odaId) {
                    return true;
                }

                return selectedCount < oda.ayrilanKisiSayisi;
            })
            .map((oda) => ({
                value: oda.odaId,
                label: `${oda.odaNo} - ${oda.binaAdi} (${oda.odaTipiAdi}, ${oda.ayrilanKisiSayisi} kisi)`
            }));
    }

    getKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): number | null {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        return atama?.odaId ?? null;
    }

    setKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number, odaId: number | null): void {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        if (atama) {
            atama.odaId = odaId;
            return;
        }

        kisi.atamalar = [...kisi.atamalar, { segmentId, odaId }];
    }

    private getSegmentOdaSelectedCount(segmentId: number, odaId: number): number {
        if (!this.konaklayanPlan) {
            return 0;
        }

        return this.konaklayanPlan.konaklayanlar.reduce((total, kisi) => {
            const selectedOdaId = kisi.atamalar.find((x) => x.segmentId === segmentId)?.odaId;
            return total + (selectedOdaId === odaId ? 1 : 0);
        }, 0);
    }

    getRezervasyonDetay(rezervasyonId: number | null | undefined): RezervasyonDetayDto | null {
        if (!rezervasyonId || rezervasyonId <= 0) {
            return null;
        }

        return this.rezervasyonDetayById[rezervasyonId] ?? null;
    }

    isRezervasyonDetayLoading(rezervasyonId: number | null | undefined): boolean {
        if (!rezervasyonId || rezervasyonId <= 0) {
            return false;
        }

        return this.detayLoadingByRezervasyonId[rezervasyonId] ?? false;
    }

    private loadReferences(): void {
        this.loadingReferences = true;
        forkJoin({
            tesisler: this.service.getTesisler(),
            misafirTipleri: this.service.getMisafirTipleri(),
            konaklamaTipleri: this.service.getKonaklamaTipleri()
        })
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, misafirTipleri, konaklamaTipleri }) => {
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.misafirTipleri = [...misafirTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.konaklamaTipleri = [...konaklamaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));

                    if (this.selectedTesisId && !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId && this.tesisler.length > 0) {
                        this.selectedTesisId = this.tesisler[0].id;
                    }

                    if (this.selectedMisafirTipiId && !this.misafirTipleri.some((x) => x.id === this.selectedMisafirTipiId)) {
                        this.selectedMisafirTipiId = null;
                    }

                    if (!this.selectedMisafirTipiId && this.misafirTipleri.length > 0) {
                        this.selectedMisafirTipiId = this.misafirTipleri[0].id;
                    }

                    if (this.selectedKonaklamaTipiId && !this.konaklamaTipleri.some((x) => x.id === this.selectedKonaklamaTipiId)) {
                        this.selectedKonaklamaTipiId = null;
                    }

                    if (!this.selectedKonaklamaTipiId && this.konaklamaTipleri.length > 0) {
                        this.selectedKonaklamaTipiId = this.konaklamaTipleri[0].id;
                    }

                    if (this.selectedTesisId && this.selectedTesisId > 0) {
                        this.applySelectedTesisDateTimes();
                        this.loadOdaTipleri(this.selectedTesisId);
                        this.loadRezervasyonKayitlari(this.selectedTesisId);
                        return;
                    }

                    this.odaTipleri = [];
                    this.selectedOdaTipiId = null;
                    this.senaryolar = [];
                    this.rezervasyonKayitlari = [];
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.misafirTipleri = [];
                    this.konaklamaTipleri = [];
                    this.selectedMisafirTipiId = null;
                    this.selectedKonaklamaTipiId = null;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisler = [];
                    this.odaTipleri = [];
                    this.misafirTipleri = [];
                    this.konaklamaTipleri = [];
                    this.selectedTesisId = null;
                    this.selectedOdaTipiId = null;
                    this.selectedMisafirTipiId = null;
                    this.selectedKonaklamaTipiId = null;
                    this.senaryolar = [];
                    this.rezervasyonKayitlari = [];
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadRezervasyonKayitlari(tesisId: number | null): void {
        this.loadingRezervasyonlar = true;
        this.service
            .getRezervasyonKayitlari(tesisId)
            .pipe(
                finalize(() => {
                    this.loadingRezervasyonlar = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.rezervasyonKayitlari = items;
                    this.checkActionLoadingByRezervasyonId = {};
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.closeRezervasyonUcretDetayDialog();
                    this.closeDegisiklikGecmisiDialog();
                    this.closeKonaklayanPlaniDialog();
                    this.closeOdaDegisimDialog();
                    this.closeOdemeDialog();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.rezervasyonKayitlari = [];
                    this.checkActionLoadingByRezervasyonId = {};
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.closeRezervasyonUcretDetayDialog();
                    this.closeDegisiklikGecmisiDialog();
                    this.closeKonaklayanPlaniDialog();
                    this.closeOdaDegisimDialog();
                    this.closeOdemeDialog();
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadDegisiklikGecmisi(rezervasyonId: number): void {
        this.degisiklikGecmisiLoading = true;
        this.service
            .getDegisiklikGecmisi(rezervasyonId)
            .pipe(
                finalize(() => {
                    this.degisiklikGecmisiLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.degisiklikGecmisiKayitlari = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.degisiklikGecmisiKayitlari = [];
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadKonaklayanPlani(rezervasyonId: number): void {
        this.konaklayanPlanLoading = true;
        this.service
            .getKonaklayanPlani(rezervasyonId)
            .pipe(
                finalize(() => {
                    this.konaklayanPlanLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (plan) => {
                    this.konaklayanPlan = plan;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.konaklayanPlan = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadOdemeOzeti(rezervasyonId: number): void {
        this.odemeLoading = true;
        this.service
            .getOdemeOzeti(rezervasyonId)
            .pipe(
                finalize(() => {
                    this.odemeLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (ozet) => {
                    this.odemeOzeti = ozet;
                    this.odemeTutari = ozet.kalanTutar > 0 ? ozet.kalanTutar : null;
                    const kayit = this.rezervasyonKayitlari.find((x) => x.id === rezervasyonId);
                    if (kayit) {
                        kayit.odenenTutar = ozet.odenenTutar;
                        kayit.kalanTutar = ozet.kalanTutar;
                    }
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.odemeOzeti = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadRezervasyonDetay(rezervasyonId: number): void {
        if (rezervasyonId <= 0 || this.rezervasyonDetayById[rezervasyonId] || this.detayLoadingByRezervasyonId[rezervasyonId]) {
            return;
        }

        this.detayLoadingByRezervasyonId[rezervasyonId] = true;
        this.service
            .getRezervasyonDetay(rezervasyonId)
            .pipe(
                finalize(() => {
                    this.detayLoadingByRezervasyonId[rezervasyonId] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (detay) => {
                    this.rezervasyonDetayById[rezervasyonId] = detay;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadOdaTipleri(tesisId: number, clearResults = false): void {
        this.loadingReferences = true;
        this.service
            .getOdaTipleriByTesis(tesisId)
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (odaTipleri) => {
                    this.odaTipleri = [...odaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    if (this.selectedOdaTipiId && !this.odaTipleri.some((x) => x.id === this.selectedOdaTipiId)) {
                        this.selectedOdaTipiId = null;
                    }

                    if (clearResults) {
                        this.senaryolar = [];
                    }

                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.odaTipleri = [];
                    this.selectedOdaTipiId = null;
                    this.senaryolar = [];
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private updateRezervasyonDurumu(rezervasyonId: number, durum: string): void {
        const kayit = this.rezervasyonKayitlari.find((x) => x.id === rezervasyonId);
        if (kayit) {
            kayit.rezervasyonDurumu = durum;
        }

        const detay = this.rezervasyonDetayById[rezervasyonId];
        if (detay) {
            detay.rezervasyonDurumu = durum;
        }
    }

    formatDateTime(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return date.toLocaleString('tr-TR');
    }

    formatCurrency(value: number, currency: string): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        const safeCurrency = (currency || 'TRY').toUpperCase();
        return `${safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${safeCurrency}`;
    }

    getRezervasyonDurumLabel(durum: string): string {
        switch (durum) {
            case this.durumTaslak:
                return 'Taslak';
            case this.durumOnayli:
                return 'Onaylı';
            case this.durumCheckInTamamlandi:
                return 'Giriş Yapıldı';
            case this.durumCheckOutTamamlandi:
                return 'Çıkış Yapıldı';
            case this.durumIptal:
                return 'İptal';
            default:
                return durum;
        }
    }

    getRezervasyonDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (durum) {
            case this.durumTaslak:
                return 'secondary';
            case this.durumOnayli:
                return 'info';
            case this.durumCheckInTamamlandi:
                return 'warn';
            case this.durumCheckOutTamamlandi:
                return 'success';
            case this.durumIptal:
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private nowInput(): string {
        return this.toDateTimeLocalInput(new Date());
    }

    private tomorrowInput(): string {
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        return this.toDateTimeLocalInput(tomorrow);
    }

    private toIsoDate(value: string): string {
        if (value.length === 16) {
            return `${value}:00`;
        }

        return value;
    }

    private normalizeOptional(value: string): string | null {
        const normalized = value.trim();
        return normalized.length > 0 ? normalized : null;
    }

    private toDateTimeLocalInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}`;
    }

    private applySelectedTesisDateTimes(): void {
        const selectedTesis = this.tesisler.find((x) => x.id === this.selectedTesisId);
        if (!selectedTesis) {
            return;
        }

        const [girisSaat, girisDakika] = this.parseSaat(selectedTesis.girisSaati, this.defaultGirisSaati);
        const [cikisSaat, cikisDakika] = this.parseSaat(selectedTesis.cikisSaati, this.defaultCikisSaati);

        const baslangic = this.tryParseDate(this.baslangicTarihi) ?? new Date();
        const bitis = this.tryParseDate(this.bitisTarihi) ?? new Date(baslangic.getTime() + 24 * 60 * 60 * 1000);

        baslangic.setHours(girisSaat, girisDakika, 0, 0);
        bitis.setHours(cikisSaat, cikisDakika, 0, 0);

        if (bitis.getTime() <= baslangic.getTime()) {
            bitis.setDate(bitis.getDate() + 1);
        }

        this.baslangicTarihi = this.toDateTimeLocalInput(baslangic);
        this.bitisTarihi = this.toDateTimeLocalInput(bitis);
    }

    private parseSaat(source: string | null | undefined, fallback: string): [number, number] {
        const normalized = (source && source.trim().length > 0 ? source : fallback).trim();
        const [rawSaat, rawDakika] = normalized.split(':');
        const saat = Number.parseInt(rawSaat ?? '', 10);
        const dakika = Number.parseInt(rawDakika ?? '', 10);
        const safeSaat = Number.isFinite(saat) && saat >= 0 && saat <= 23 ? saat : 0;
        const safeDakika = Number.isFinite(dakika) && dakika >= 0 && dakika <= 59 ? dakika : 0;
        return [safeSaat, safeDakika];
    }

    private tryParseDate(value: string): Date | null {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return null;
        }

        return date;
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import {
    KonaklamaSenaryoDto,
    RezervasyonDetayDto,
    RezervasyonIndirimKuraliSecenekDto,
    RezervasyonKaydetRequestDto,
    RezervasyonKonaklamaTipiDto,
    RezervasyonKonaklayanKisiDto,
    RezervasyonKonaklayanPlanDto,
    RezervasyonKonaklayanSegmentDto,
    RezervasyonListeDto,
    RezervasyonMisafirTipiDto,
    RezervasyonOdaTipiDto,
    RezervasyonTesisDto,
    SenaryoFiyatHesaplamaSonucuDto
} from './rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from './rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, ToastModule, ToolbarModule],
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
    konaklayanPlanDialogVisible = false;
    konaklayanPlanLoading = false;
    konaklayanPlanSaving = false;
    konaklayanPlanRezervasyonId: number | null = null;
    konaklayanPlanReferansNo = '';
    konaklayanPlan: RezervasyonKonaklayanPlanDto | null = null;

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Konaklayan plani kaydedildi.' });
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
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.closeRezervasyonUcretDetayDialog();
                    this.closeKonaklayanPlaniDialog();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.rezervasyonKayitlari = [];
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.closeRezervasyonUcretDetayDialog();
                    this.closeKonaklayanPlaniDialog();
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

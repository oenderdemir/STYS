import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, finalize, forkJoin, Observable, switchMap } from 'rxjs';
import { ConfirmationService, MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { EkHizmetPaketCakismaPolitikalari } from '../tesis-yonetimi/ek-hizmet-paket-cakisma-politikasi.constants';
import { KonaklayanKatilimDurumlari } from './konaklayan-katilim-durumlari.constants';
import { KonaklayanCinsiyetleri } from './konaklayan-cinsiyetleri.constants';
import {
    KonaklamaSenaryoDto,
    RezervasyonCheckInKontrolDto,
    RezervasyonDetayDto,
    RezervasyonDegisiklikGecmisiDto,
    RezervasyonEkHizmetDto,
    RezervasyonEkHizmetMisafirSecenekDto,
    RezervasyonEkHizmetSecenekleriDto,
    RezervasyonEkHizmetTarifeSecenekDto,
    RezervasyonIndirimKuraliSecenekDto,
    RezervasyonKaydetRequestDto,
    RezervasyonKayitSonucDto,
    RezervasyonKonaklamaHakkiDto,
    RezervasyonKonaklamaHakkiTuketimKaydiDto,
    RezervasyonKonaklamaHakkiTuketimNoktasiDto,
    RezervasyonKonaklamaTipiDto,
    RezervasyonKonaklayanKisiDto,
    RezervasyonKonaklayanOdaSecenekDto,
    RezervasyonKonaklayanPlanDto,
    RezervasyonKonaklayanSegmentDto,
    RezervasyonListeDto,
    RezervasyonMisafirTipiDto,
    RezervasyonOdemeOzetDto,
    RezervasyonOdaDegisimKayitDto,
    RezervasyonOdaDegisimAdayOdaDto,
    RezervasyonOdaDegisimKonaklayanDto,
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
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, ConfirmDialogModule, DialogModule, InputTextModule, MenuModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule, TooltipModule],
    templateUrl: './rezervasyon-yonetimi.html',
    styles: [`
        :host ::ng-deep .rez-odeme-dialog .p-dialog-content {
            padding-top: 0.5rem;
        }

        :host ::ng-deep .rez-odeme-dialog .p-dialog-footer {
            border-top: 1px solid var(--surface-border);
            padding-top: 1rem;
        }

        .rez-odeme-shell {
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }

        .rez-odeme-summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 0.75rem;
        }

        .rez-odeme-summary-card {
            border: 1px solid var(--surface-border);
            border-radius: 14px;
            padding: 0.9rem 1rem;
            background: linear-gradient(180deg, color-mix(in srgb, var(--surface-card) 88%, white), var(--surface-ground));
            box-shadow: 0 4px 18px rgba(15, 23, 42, 0.05);
        }

        .rez-odeme-summary-card.is-primary {
            background: linear-gradient(135deg, color-mix(in srgb, var(--primary-color) 14%, white), color-mix(in srgb, var(--primary-color) 6%, var(--surface-card)));
            border-color: color-mix(in srgb, var(--primary-color) 28%, var(--surface-border));
        }

        .rez-odeme-summary-card.is-accent {
            background: linear-gradient(135deg, #ecfdf5, #f0fdf4);
            border-color: #bbf7d0;
        }

        .rez-odeme-summary-label {
            font-size: 0.78rem;
            color: var(--text-color-secondary);
            margin-bottom: 0.35rem;
        }

        .rez-odeme-summary-value {
            font-size: 1.15rem;
            font-weight: 700;
            line-height: 1.2;
            color: var(--text-color);
        }

        .rez-odeme-panel {
            border: 1px solid var(--surface-border);
            border-radius: 16px;
            background: var(--surface-card);
            padding: 1rem;
        }

        .rez-odeme-panel-header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 0.75rem;
            margin-bottom: 0.85rem;
        }

        .rez-odeme-panel-header.compact {
            margin-bottom: 0;
        }

        .rez-odeme-panel-title {
            font-size: 1rem;
            font-weight: 700;
            color: var(--text-color);
        }

        .rez-odeme-panel-subtitle {
            font-size: 0.82rem;
            color: var(--text-color-secondary);
        }

        .rez-odeme-action-row {
            display: flex;
            flex-wrap: wrap;
            justify-content: flex-end;
            gap: 0.5rem;
        }

        .rez-odeme-table :is(th, td) {
            vertical-align: top;
        }

        .rez-odeme-table-cell {
            white-space: normal;
            word-break: break-word;
            line-height: 1.35;
        }

        .rez-odeme-history-table :is(th, td) {
            vertical-align: top;
        }
    `],
    providers: [MessageService, ConfirmationService]
})
export class RezervasyonYonetimi implements OnInit {
    private readonly service = inject(RezervasyonYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
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
    readonly konaklayanCinsiyetSecenekleri = [
        { label: 'Kadin', value: KonaklayanCinsiyetleri.Kadin },
        { label: 'Erkek', value: KonaklayanCinsiyetleri.Erkek }
    ];
    readonly konaklayanKatilimDurumuSecenekleri = [
        { label: 'Bekleniyor', value: KonaklayanKatilimDurumlari.Bekleniyor },
        { label: 'Geldi', value: KonaklayanKatilimDurumlari.Geldi },
        { label: 'Gelmedi', value: KonaklayanKatilimDurumlari.Gelmedi },
        { label: 'Ayrildi', value: KonaklayanKatilimDurumlari.Ayrildi }
    ];
    odemeDialogVisible = false;
    odemeLoading = false;
    odemeSaving = false;
    odemeRezervasyonId: number | null = null;
    odemeReferansNo = '';
    odemeOzeti: RezervasyonOdemeOzetDto | null = null;
    odemeEkHizmetPanelExpanded = false;
    odemeEkHizmetSecenekleri: RezervasyonEkHizmetSecenekleriDto | null = null;
    selectedEkHizmetKonaklayanId: number | null = null;
    selectedEkHizmetTarifeId: number | null = null;
    ekHizmetTarihi = '';
    ekHizmetMiktar: number | null = 1;
    ekHizmetBirimFiyat: number | null = null;
    ekHizmetAciklama = '';
    ekHizmetSaving = false;
    editingEkHizmetId: number | null = null;
    odemeTutari: number | null = null;
    odemeTipi = 'Nakit';
    odemeAciklama = '';
    odaDegisimDialogVisible = false;
    odaDegisimLoading = false;
    odaDegisimSaving = false;
    odaDegisimRezervasyonId: number | null = null;
    odaDegisimReferansNo = '';
    odaDegisimRezervasyonDurumu: string | null = null;
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
    senaryoKonaklayanCinsiyetleri: Array<string | null> = [null];
    baslangicTarihi = this.nowInput();
    bitisTarihi = this.tomorrowInput();
    misafirAdiSoyadi = '';
    misafirTelefon = '';
    misafirEposta = '';
    tcKimlikNo = '';
    pasaportNo = '';
    misafirCinsiyeti: string | null = null;
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
    private readonly hakDurumBekliyor = 'Bekliyor';
    private readonly hakDurumKullanildi = 'Kullanildi';
    private readonly hakDurumIptal = 'Iptal';
    updatingKonaklamaHakId: number | null = null;
    konaklamaHakkiTuketimDialogVisible = false;
    selectedKonaklamaHakkiTuketimRezervasyonId: number | null = null;
    selectedKonaklamaHakkiTuketimHakId: number | null = null;
    konaklamaHakkiTuketimTarihi = this.nowInput();
    konaklamaHakkiTuketimMiktar: number | null = 1;
    selectedKonaklamaHakkiTuketimNoktasiId: number | null = null;
    konaklamaHakkiTuketimAciklama = '';
    savingKonaklamaHakkiTuketim = false;
    removingKonaklamaHakkiTuketimId: number | null = null;
    konaklamaHakkiGecmisDialogVisible = false;
    selectedKonaklamaHakkiGecmisRezervasyonId: number | null = null;
    selectedKonaklamaHakkiGecmisHakId: number | null = null;

    get canView(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.Manage');
    }

    get canApplyCustomDiscount(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.CustomIndirimGirebilir');
    }

    get ekHizmetMisafirSecenekleri(): RezervasyonEkHizmetMisafirSecenekDto[] {
        return this.odemeEkHizmetSecenekleri?.misafirler ?? [];
    }

    get ekHizmetTarifeSecenekleri(): RezervasyonEkHizmetTarifeSecenekDto[] {
        return this.odemeEkHizmetSecenekleri?.tarifeler ?? [];
    }

    get canModifyEkHizmet(): boolean {
        const durum = this.getSelectedOdemeRezervasyonDurumu();
        return !!this.odemeOzeti && durum === this.durumCheckInTamamlandi;
    }

    get ekHizmetBilgiMesaji(): string | null {
        const reason = this.getEkHizmetModificationDisabledReason();
        if (reason === 'Check-in bekleniyor') {
            return 'Ek hizmet kalemleri yalnizca check-in tamamlandiktan sonra eklenebilir veya guncellenebilir.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemleri yalnizca goruntulenebilir.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemleri yalnizca goruntulenebilir.';
        }

        if ((this.odemeOzeti?.odenenTutar ?? 0) > 0) {
            return 'Odeme alinmis rezervasyonda ek hizmet eklenebilir. Ancak silme veya tutar dusuren guncelleme fazla tahsilat olusturacaksa engellenir.';
        }

        return null;
    }

    get odemeEkHizmetToggleLabel(): string {
        return this.odemeEkHizmetPanelExpanded ? 'Ek Hizmetleri Gizle' : 'Ek Hizmetleri Goster';
    }

    get odaDegisimBilgiMesaji(): string | null {
        if (this.odaDegisimRezervasyonDurumu === this.durumCheckInTamamlandi) {
            return 'Bu rezervasyon check-in yapmis durumda. Oda degisimi kaydedildiginde ilgili konaklayan atamalari yeni odaya otomatik tasinacaktir.';
        }

        if (this.odaDegisimRezervasyonDurumu === this.durumTaslak || this.odaDegisimRezervasyonDurumu === this.durumOnayli) {
            return 'Bu ekran check-in oncesi planlanan oda atamasini gucellemek icin kullanilir.';
        }

        return null;
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
            this.konaklamaTipleri = [];
            this.selectedOdaTipiId = null;
            this.selectedKonaklamaTipiId = null;
            this.senaryolar = [];
            this.rezervasyonKayitlari = [];
            this.availableDiscountRules = [];
            return;
        }

        this.applySelectedTesisDateTimes();
        this.loadOdaTipleri(this.selectedTesisId, true);
        this.loadKonaklamaTipleriByTesis(this.selectedTesisId);
        this.loadRezervasyonKayitlari(this.selectedTesisId);
    }

    search(): void {
        if (!this.canView) {
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir tesis seciniz.' });
            return;
        }

        if (!this.selectedMisafirTipiId || this.selectedMisafirTipiId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir misafir tipi seciniz.' });
            return;
        }

        if (!this.selectedKonaklamaTipiId || this.selectedKonaklamaTipiId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir konaklama tipi seciniz.' });
            return;
        }

        if (this.kisiSayisi <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kisi sayisi sifirdan buyuk olmalidir.' });
            return;
        }

        this.syncSenaryoKonaklayanCinsiyetleri();
        if (this.senaryoKonaklayanCinsiyetleri.some((x) => !x)) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen senaryo aramasi icin tum konaklayanlarin cinsiyetini seciniz.' });
            return;
        }

        if (!this.baslangicTarihi || !this.bitisTarihi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Baslangic ve bitis tarihi zorunludur.' });
            return;
        }

        if (new Date(this.baslangicTarihi).getTime() >= new Date(this.bitisTarihi).getTime()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Baslangic tarihi bitis tarihinden kucuk olmalidir.' });
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
                bitisTarihi: this.toIsoDate(this.bitisTarihi),
                konaklayanCinsiyetleri: this.senaryoKonaklayanCinsiyetleri.map((x) => this.normalizeOptional(x ?? ''))
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    onKisiSayisiChange(): void {
        if (!Number.isFinite(this.kisiSayisi)) {
            this.kisiSayisi = 1;
        }

        this.kisiSayisi = Math.max(1, Math.trunc(this.kisiSayisi));
        this.syncSenaryoKonaklayanCinsiyetleri();
    }

    getSenaryoKonaklayanCinsiyetLabel(index: number): string {
        return `${index + 1}. Kisi`;
    }

    onSenaryoKonaklayanCinsiyetChange(index: number): void {
        if (index === 0 && !this.misafirCinsiyeti) {
            this.misafirCinsiyeti = this.senaryoKonaklayanCinsiyetleri[0] ?? null;
        }
    }

    getScenarioSharedRoomGenderBadgeLabel(scenario: KonaklamaSenaryoDto): string | null {
        if (!this.hasSharedRoomUsage(scenario)) {
            return null;
        }

        const normalizedGenders = this.senaryoKonaklayanCinsiyetleri.filter((x): x is string => !!x);
        const uniqueGenders = [...new Set(normalizedGenders)];
        if (uniqueGenders.length === 1) {
            return `Paylasimli Oda / ${uniqueGenders[0]} uyumlu`;
        }

        return 'Paylasimli Oda / Cinsiyet uyumu saglandi';
    }

    getScenarioSharedRoomGenderBadgeSeverity(scenario: KonaklamaSenaryoDto): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        return this.hasSharedRoomUsage(scenario) ? UiSeverity.Success : UiSeverity.Secondary;
    }

    private syncSenaryoKonaklayanCinsiyetleri(): void {
        const targetCount = Math.max(1, Math.trunc(this.kisiSayisi || 1));
        if (this.senaryoKonaklayanCinsiyetleri.length === targetCount) {
            if (!this.misafirCinsiyeti && this.senaryoKonaklayanCinsiyetleri.length > 0) {
                this.misafirCinsiyeti = this.senaryoKonaklayanCinsiyetleri[0];
            }
            return;
        }

        this.senaryoKonaklayanCinsiyetleri = Array.from({ length: targetCount }, (_, index) => this.senaryoKonaklayanCinsiyetleri[index] ?? null);
        if (!this.misafirCinsiyeti && this.senaryoKonaklayanCinsiyetleri.length > 0) {
            this.misafirCinsiyeti = this.senaryoKonaklayanCinsiyetleri[0];
        }
    }

    reserveScenario(scenario: KonaklamaSenaryoDto): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tesis secimi zorunludur.' });
            return;
        }

        if (!this.misafirAdiSoyadi.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Misafir adi soyadi zorunludur.' });
            return;
        }

        if (!this.misafirTelefon.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Misafir telefonu zorunludur.' });
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
            misafirCinsiyeti: this.misafirCinsiyeti ?? this.senaryoKonaklayanCinsiyetleri[0] ?? null,
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
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `Rezervasyon kaydedildi. Referans: ${result.referansNo}`
                    });
                    this.loadRezervasyonKayitlari(this.selectedTesisId);
                    this.search();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    openDiscountDialog(scenario: KonaklamaSenaryoDto): void {
        if (!this.selectedTesisId || !this.selectedMisafirTipiId || !this.selectedKonaklamaTipiId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tesis, misafir tipi ve konaklama tipi secimi zorunludur.' });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    hesaplaSenaryoUcreti(): void {
        if (!this.selectedScenarioForDiscount || this.calculatingScenarioPrice) {
            return;
        }

        if ((this.customDiscountAmount ?? 0) < 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Tutar', detail: 'Custom indirim tutari sifirdan kucuk olamaz.' });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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

    getKonaklamaHakkiDurumLabel(durum: string): string {
        switch (durum) {
            case this.hakDurumBekliyor:
                return 'Bekliyor';
            case this.hakDurumKullanildi:
                return 'Kullanıldı';
            case this.hakDurumIptal:
                return 'İptal';
            default:
                return durum;
        }
    }

    getKonaklamaHakkiDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (durum) {
            case this.hakDurumBekliyor:
                return UiSeverity.Warn;
            case this.hakDurumKullanildi:
                return UiSeverity.Success;
            case this.hakDurumIptal:
                return UiSeverity.Danger;
            default:
                return UiSeverity.Secondary;
        }
    }

    canGuncelleKonaklamaHakkiDurumu(detay: RezervasyonDetayDto | null, hak: RezervasyonKonaklamaHakkiDto): boolean {
        return !!detay
            && this.canManage
            && detay.rezervasyonDurumu === this.durumCheckInTamamlandi
            && hak.tuketimKayitlari.length === 0
            && (hak.durum === this.hakDurumBekliyor || hak.durum === this.hakDurumKullanildi);
    }

    canOpenKonaklamaHakkiTuketim(detay: RezervasyonDetayDto | null, hak: RezervasyonKonaklamaHakkiDto): boolean {
        return !!detay
            && this.canManage
            && detay.rezervasyonDurumu === this.durumCheckInTamamlandi
            && hak.durum !== this.hakDurumIptal
            && (hak.kullanimTipi !== 'Adetli' || (hak.kalanMiktar ?? 0) > 0);
    }

    canOpenKonaklamaHakkiGecmisi(hak: RezervasyonKonaklamaHakkiDto): boolean {
        return hak.tuketimKayitlari.length > 0;
    }

    getKonaklamaHakkiAksiyonLabel(durum: string): string {
        return durum === this.hakDurumBekliyor ? 'Kullanildi' : 'Geri Al';
    }

    getKonaklamaHakkiAksiyonIkonu(durum: string): string {
        return durum === this.hakDurumBekliyor ? 'pi pi-check' : 'pi pi-undo';
    }

    guncelleKonaklamaHakkiDurumu(rezervasyonId: number, hakId: number, mevcutDurum: string): void {
        const detay = this.getRezervasyonDetay(rezervasyonId);
        const hak = detay?.konaklamaHaklari.find((x) => x.id === hakId) ?? null;
        if (!hak || !this.canGuncelleKonaklamaHakkiDurumu(detay, hak)) {
            return;
        }

        const hedefDurum = mevcutDurum === this.hakDurumBekliyor
            ? this.hakDurumKullanildi
            : this.hakDurumBekliyor;

        this.updatingKonaklamaHakId = hakId;
        this.service
            .guncelleKonaklamaHakkiDurumu(rezervasyonId, hakId, { durum: hedefDurum })
            .pipe(
                finalize(() => {
                    this.updatingKonaklamaHakId = null;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updatedDetay) => {
                    this.rezervasyonDetayById[rezervasyonId] = updatedDetay;
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: hedefDurum === this.hakDurumKullanildi
                            ? 'Konaklama hakki kullanildi olarak isaretlendi.'
                            : 'Konaklama hakki tekrar bekliyor durumuna alindi.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    openKonaklamaHakkiTuketimDialog(rezervasyonId: number, hak: RezervasyonKonaklamaHakkiDto): void {
        const detay = this.getRezervasyonDetay(rezervasyonId);
        if (!this.canOpenKonaklamaHakkiTuketim(detay, hak)) {
            return;
        }

        this.selectedKonaklamaHakkiTuketimRezervasyonId = rezervasyonId;
        this.selectedKonaklamaHakkiTuketimHakId = hak.id;
        this.konaklamaHakkiTuketimDialogVisible = true;
        this.konaklamaHakkiTuketimMiktar = hak.kullanimTipi === 'Sinirsiz' ? 1 : Math.max(1, hak.kalanMiktar ?? 1);
        this.konaklamaHakkiTuketimTarihi = this.nowInput();
        this.selectedKonaklamaHakkiTuketimNoktasiId = hak.tuketimNoktalari[0]?.id ?? null;
        this.konaklamaHakkiTuketimAciklama = '';
    }

    closeKonaklamaHakkiTuketimDialog(): void {
        this.konaklamaHakkiTuketimDialogVisible = false;
        this.selectedKonaklamaHakkiTuketimRezervasyonId = null;
        this.selectedKonaklamaHakkiTuketimHakId = null;
        this.konaklamaHakkiTuketimTarihi = this.nowInput();
        this.konaklamaHakkiTuketimMiktar = 1;
        this.selectedKonaklamaHakkiTuketimNoktasiId = null;
        this.konaklamaHakkiTuketimAciklama = '';
    }

    openKonaklamaHakkiGecmisDialog(rezervasyonId: number, hak: RezervasyonKonaklamaHakkiDto): void {
        if (!this.canOpenKonaklamaHakkiGecmisi(hak)) {
            return;
        }

        this.selectedKonaklamaHakkiGecmisRezervasyonId = rezervasyonId;
        this.selectedKonaklamaHakkiGecmisHakId = hak.id;
        this.konaklamaHakkiGecmisDialogVisible = true;
    }

    closeKonaklamaHakkiGecmisDialog(): void {
        this.konaklamaHakkiGecmisDialogVisible = false;
        this.selectedKonaklamaHakkiGecmisRezervasyonId = null;
        this.selectedKonaklamaHakkiGecmisHakId = null;
    }

    getSelectedKonaklamaHakkiForGecmis(): RezervasyonKonaklamaHakkiDto | null {
        const detay = this.getRezervasyonDetay(this.selectedKonaklamaHakkiGecmisRezervasyonId);
        if (!detay || !this.selectedKonaklamaHakkiGecmisHakId) {
            return null;
        }

        return detay.konaklamaHaklari.find((x) => x.id === this.selectedKonaklamaHakkiGecmisHakId) ?? null;
    }

    getKonaklamaHakkiToplamTuketimKaydiMiktari(hak: RezervasyonKonaklamaHakkiDto | null): number {
        if (!hak) {
            return 0;
        }

        return hak.tuketimKayitlari.reduce((total, item) => total + (Number(item.miktar) || 0), 0);
    }

    getSelectedKonaklamaHakkiForTuketim(): RezervasyonKonaklamaHakkiDto | null {
        const detay = this.getSelectedRezervasyonUcretDetay();
        if (!detay || !this.selectedKonaklamaHakkiTuketimHakId) {
            return null;
        }

        return detay.konaklamaHaklari.find((x) => x.id === this.selectedKonaklamaHakkiTuketimHakId) ?? null;
    }

    canSaveKonaklamaHakkiTuketim(): boolean {
        const detay = this.getSelectedRezervasyonUcretDetay();
        const hak = this.getSelectedKonaklamaHakkiForTuketim();
        return !!detay
            && !!hak
            && this.canOpenKonaklamaHakkiTuketim(detay, hak)
            && !!this.konaklamaHakkiTuketimTarihi
            && Number.isFinite(Number(this.konaklamaHakkiTuketimMiktar ?? 0))
            && Number(this.konaklamaHakkiTuketimMiktar ?? 0) > 0
            && (hak.kullanimNoktasi === 'Genel' || !!this.selectedKonaklamaHakkiTuketimNoktasiId);
    }

    kaydetKonaklamaHakkiTuketim(): void {
        const rezervasyonId = this.selectedKonaklamaHakkiTuketimRezervasyonId;
        const hakId = this.selectedKonaklamaHakkiTuketimHakId;
        if (!rezervasyonId || !hakId || !this.canSaveKonaklamaHakkiTuketim()) {
            return;
        }

        this.savingKonaklamaHakkiTuketim = true;
        this.service
            .kaydetKonaklamaHakkiTuketim(rezervasyonId, hakId, {
                isletmeAlaniId: this.selectedKonaklamaHakkiTuketimNoktasiId,
                tuketimTarihi: this.konaklamaHakkiTuketimTarihi,
                miktar: Number(this.konaklamaHakkiTuketimMiktar ?? 0),
                aciklama: this.konaklamaHakkiTuketimAciklama.trim() || null
            })
            .pipe(
                finalize(() => {
                    this.savingKonaklamaHakkiTuketim = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updatedDetay) => {
                    this.rezervasyonDetayById[rezervasyonId] = updatedDetay;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklama hakki tuketim kaydi eklendi.' });
                    this.closeKonaklamaHakkiTuketimDialog();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    silKonaklamaHakkiTuketim(rezervasyonId: number, hakId: number, kayit: RezervasyonKonaklamaHakkiTuketimKaydiDto): void {
        if (this.removingKonaklamaHakkiTuketimId !== null) {
            return;
        }

        this.removingKonaklamaHakkiTuketimId = kayit.id;
        this.service
            .silKonaklamaHakkiTuketim(rezervasyonId, hakId, kayit.id)
            .pipe(
                finalize(() => {
                    this.removingKonaklamaHakkiTuketimId = null;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (updatedDetay) => {
                    this.rezervasyonDetayById[rezervasyonId] = updatedDetay;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklama hakki tuketim kaydi silindi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getKonaklamaHakkiKalanLabel(hak: RezervasyonKonaklamaHakkiDto): string {
        return hak.kalanMiktar === null ? 'Sinirsiz' : `${hak.kalanMiktar}`;
    }

    getKonaklamaHakkiTuketimNoktasiSecenekleri(hak: RezervasyonKonaklamaHakkiDto | null): Array<{ label: string; value: number }> {
        if (!hak) {
            return [];
        }

        return hak.tuketimNoktalari.map((x: RezervasyonKonaklamaHakkiTuketimNoktasiDto) => ({
            label: `${x.ad} (${x.binaAdi})`,
            value: x.id
        }));
    }

    getSelectedKonaklamaTipi(): RezervasyonKonaklamaTipiDto | null {
        if (!this.selectedKonaklamaTipiId) {
            return null;
        }

        return this.konaklamaTipleri.find((item) => item.id === this.selectedKonaklamaTipiId) ?? null;
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
            case 'KonaklamaHaklariUretildi':
                return 'Konaklama Hakları Üretildi';
            case 'KonaklamaHakkiDurumuGuncellendi':
                return 'Konaklama Hakkı Durumu Güncellendi';
            case 'KonaklamaHakkiTuketimiKaydedildi':
                return 'Konaklama Hakkı Tüketimi Kaydedildi';
            case 'KonaklamaHakkiTuketimiSilindi':
                return 'Konaklama Hakkı Tüketimi Silindi';
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
            const cinsiyet = this.readUnknown(item, ['Cinsiyet', 'cinsiyet']);
            const katilimDurumu = this.readUnknown(item, ['KatilimDurumu', 'katilimDurumu']);
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
                    const yatakNo = this.readUnknown(atama, ['YatakNo', 'yatakNo']);
                    const yatakText = typeof yatakNo === 'undefined' || yatakNo === null
                        ? ''
                        : ` (Yatak ${this.toDisplayString(yatakNo)})`;
                    return `Segment ${this.toDisplayString(segmentId)} -> Oda ${this.toDisplayString(odaId)}${yatakText}`;
                });

            rows.push({
                siraNo: this.toDisplayString(siraNo),
                adSoyad: this.toDisplayString(adSoyad),
                cinsiyet: this.toDisplayString(cinsiyet),
                katilimDurumu: this.toDisplayString(katilimDurumu),
                tcKimlikNo: this.toDisplayString(tcKimlikNo),
                pasaportNo: this.toDisplayString(pasaportNo),
                atamalar: atamaOzetleri.length > 0 ? atamaOzetleri.join('\n') : '-'
            });
        }

        return {
            columns: [
                { field: 'siraNo', header: 'Sıra' },
                { field: 'adSoyad', header: 'Ad Soyad' },
                { field: 'cinsiyet', header: 'Cinsiyet' },
                { field: 'katilimDurumu', header: 'Katilim Durumu' },
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
                    cinsiyet: this.normalizeOptional(kisi.cinsiyet ?? ''),
                    katilimDurumu: this.normalizeOptional(kisi.katilimDurumu ?? '') ?? KonaklayanKatilimDurumlari.Bekleniyor,
                    atamalar: kisi.atamalar.map((atama) => ({
                        segmentId: atama.segmentId,
                        odaId: atama.odaId,
                        yatakNo: atama.yatakNo
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
                            kayit.gelenKonaklayanSayisi = plan.konaklayanlar.filter((x) => x.katilimDurumu === KonaklayanKatilimDurumlari.Geldi).length;
                            kayit.bekleyenKonaklayanSayisi = plan.konaklayanlar.filter((x) => x.katilimDurumu === KonaklayanKatilimDurumlari.Bekleniyor).length;
                        }
                    }
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklayan plani kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
            && (kisi.katilimDurumu ?? '').trim().length > 0
            && kisi.atamalar.length === segmentIds.size
            && kisi.atamalar.every((atama) => {
                if (!segmentIds.has(atama.segmentId)) {
                    return false;
                }

                if (!this.isKonaklayanAssignmentsRequired(kisi)) {
                    return (atama.odaId ?? 0) <= 0 && (atama.yatakNo ?? 0) <= 0;
                }

                if ((atama.odaId ?? 0) <= 0) {
                    return false;
                }

                const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(atama.segmentId, atama.odaId);
                if (!odaSecenegi) {
                    return false;
                }

                if (!odaSecenegi.paylasimliMi) {
                    return true;
                }

                const yatakNo = atama.yatakNo ?? 0;
                return yatakNo > 0 && yatakNo <= odaSecenegi.ayrilanKisiSayisi;
            }));
    }

    isKonaklayanAssignmentsRequired(kisi: RezervasyonKonaklayanKisiDto): boolean {
        return kisi.katilimDurumu !== KonaklayanKatilimDurumlari.Gelmedi;
    }

    setKonaklayanKatilimDurumu(kisi: RezervasyonKonaklayanKisiDto, katilimDurumu: string | null): void {
        kisi.katilimDurumu = katilimDurumu ?? KonaklayanKatilimDurumlari.Bekleniyor;
        if (kisi.katilimDurumu === KonaklayanKatilimDurumlari.Gelmedi) {
            kisi.atamalar = kisi.atamalar.map((x) => ({ ...x, odaId: null, yatakNo: null }));
        }
    }

    canCompleteCheckIn(kayit: RezervasyonListeDto): boolean {
        return this.getCheckInDisabledReason(kayit) === null;
    }

    canCompleteCheckOut(kayit: RezervasyonListeDto): boolean {
        return this.getCheckOutDisabledReason(kayit) === null;
    }

    isCheckActionLoading(rezervasyonId: number): boolean {
        return this.checkActionLoadingByRezervasyonId[rezervasyonId] ?? false;
    }

    canCancelReservation(kayit: RezervasyonListeDto): boolean {
        if (!this.canManage) {
            return false;
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return true;
        }

        return kayit.rezervasyonDurumu !== this.durumCheckOutTamamlandi && (kayit.odenenTutar ?? 0) <= 0;
    }

    getCheckInDisabledReason(kayit: RezervasyonListeDto): string | null {
        if (!this.canManage) {
            return 'Yonetim yetkisi yok';
        }

        if (kayit.rezervasyonDurumu === this.durumCheckOutTamamlandi) {
            return 'Check-out tamamlandi';
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return 'Iptal edildi';
        }

        if (kayit.rezervasyonDurumu !== this.durumTaslak && kayit.rezervasyonDurumu !== this.durumOnayli) {
            return 'Durum uygun degil';
        }

        if (!kayit.konaklayanPlaniTamamlandi) {
            return 'Plan eksik';
        }

        if (kayit.odaDegisimiGerekli) {
            return 'Oda degisimi gerekli';
        }

        if ((kayit.gelenKonaklayanSayisi ?? 0) <= 0) {
            return 'Gelen misafir yok';
        }

        return null;
    }

    getCheckInDisabledMessage(kayit: RezervasyonListeDto): string {
        const reason = this.getCheckInDisabledReason(kayit);
        if (reason === 'Plan eksik') {
            return 'Check-in icin once konaklayan plani ve oda atamalari tamamlanmalidir.';
        }

        if (reason === 'Oda degisimi gerekli') {
            return 'Bu rezervasyonda ariza veya blokaj nedeniyle oda degisimi gerektigi icin once yeni oda atanmalidir.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyon icin tekrar check-in yapilamaz.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyon icin check-in yapilamaz.';
        }

        if (reason === 'Durum uygun degil') {
            return 'Check-in yalnizca Taslak veya Onayli durumundaki rezervasyonlarda baslatilabilir.';
        }

        if (reason === 'Yonetim yetkisi yok') {
            return 'Bu islem icin yetkiniz bulunmuyor.';
        }

        if (reason === 'Gelen misafir yok') {
            return 'Check-in icin en az bir konaklayan Geldi olarak isaretlenmelidir.';
        }

        return 'Check-in islemi su anda baslatilamaz.';
    }

    getCheckOutDisabledReason(kayit: RezervasyonListeDto): string | null {
        if (!this.canManage) {
            return 'Yonetim yetkisi yok';
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return 'Iptal edildi';
        }

        if (kayit.rezervasyonDurumu !== this.durumCheckInTamamlandi && kayit.rezervasyonDurumu !== this.durumCheckOutTamamlandi) {
            return 'Check-in bekleniyor';
        }

        if (kayit.rezervasyonDurumu === this.durumCheckOutTamamlandi) {
            return 'Check-out tamamlandi';
        }

        if (kayit.rezervasyonDurumu !== this.durumCheckInTamamlandi) {
            return 'Once check-in yapilmali';
        }

        if ((kayit.kalanTutar ?? 0) > 0) {
            return 'Kalan odeme var';
        }

        if ((kayit.bekleyenKonaklayanSayisi ?? 0) > 0) {
            return 'Bekleyen misafirler var';
        }

        return null;
    }

    getCheckOutDisabledMessage(kayit: RezervasyonListeDto): string {
        const reason = this.getCheckOutDisabledReason(kayit);
        if (reason === 'Kalan odeme var') {
            return `Check-out icin once kalan odeme tamamlanmalidir. Kalan tutar: ${this.formatCurrency(kayit.kalanTutar ?? 0, kayit.paraBirimi)}.`;
        }

        if (reason === 'Once check-in yapilmali') {
            return 'Check-out yapabilmek icin once check-in tamamlanmis olmalidir.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyon yeniden kapatilamaz.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyon icin check-out yapilamaz.';
        }

        if (reason === 'Yonetim yetkisi yok') {
            return 'Bu islem icin yetkiniz bulunmuyor.';
        }

        if (reason === 'Bekleyen misafirler var') {
            return 'Check-out oncesi bekleyen konaklayanlar Geldi veya Gelmedi olarak netlestirilmelidir.';
        }

        return 'Check-out islemi su anda baslatilamaz.';
    }

    getCancelReservationDisabledReason(kayit: RezervasyonListeDto): string | null {
        if (!this.canManage) {
            return 'Yonetim yetkisi yok';
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return null;
        }

        if (kayit.rezervasyonDurumu === this.durumCheckOutTamamlandi) {
            return 'Check-out tamamlandi';
        }

        if ((kayit.odenenTutar ?? 0) > 0) {
            return 'Odeme alindi';
        }

        return null;
    }

    getCancelReservationDisabledMessage(kayit: RezervasyonListeDto): string {
        const reason = this.getCancelReservationDisabledReason(kayit);
        if (reason === 'Odeme alindi') {
            return 'Bu rezervasyon icin odeme alindigi icin dogrudan iptal edilemez. Once iade veya mahsup islemi yapilmalidir.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyon iptal edilemez.';
        }

        if (reason === 'Yonetim yetkisi yok') {
            return 'Bu islem icin yetkiniz bulunmuyor.';
        }

        return 'Rezervasyon iptal edilemez.';
    }

    canOpenPaymentDialog(kayit: RezervasyonListeDto): boolean {
        return this.getPaymentDisabledReason(kayit) === null;
    }

    canOpenOdaDegisimDialog(kayit: RezervasyonListeDto): boolean {
        return this.getRoomChangeDisabledReason(kayit) === null;
    }

    getPaymentDisabledReason(kayit: RezervasyonListeDto): string | null {
        if (!this.canManage) {
            return 'Yonetim yetkisi yok';
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return 'Iptal edildi';
        }

        if (kayit.rezervasyonDurumu !== this.durumCheckInTamamlandi && kayit.rezervasyonDurumu !== this.durumCheckOutTamamlandi) {
            return 'Check-in bekleniyor';
        }

        return null;
    }

    getPaymentDisabledMessage(kayit: RezervasyonListeDto): string {
        const reason = this.getPaymentDisabledReason(kayit);
        if (reason === 'Check-in bekleniyor') {
            return 'Odeme ve ek hizmet islemleri yalnizca check-in tamamlandiktan sonra acilabilir.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyon icin odeme islemi yapilamaz.';
        }

        if (reason === 'Yonetim yetkisi yok') {
            return 'Bu islem icin yetkiniz bulunmuyor.';
        }

        return 'Odeme islemi su anda acilamaz.';
    }

    getRoomChangeDisabledReason(kayit: RezervasyonListeDto): string | null {
        if (!this.canManage) {
            return 'Yonetim yetkisi yok';
        }

        if (!kayit.odaDegisimiGerekli) {
            return 'Aktif ihtiyac yok';
        }

        if (kayit.rezervasyonDurumu === this.durumIptal) {
            return 'Iptal edildi';
        }

        if (kayit.rezervasyonDurumu === this.durumCheckOutTamamlandi) {
            return 'Check-out tamamlandi';
        }

        if (
            kayit.rezervasyonDurumu !== this.durumTaslak
            && kayit.rezervasyonDurumu !== this.durumOnayli
            && kayit.rezervasyonDurumu !== this.durumCheckInTamamlandi
        ) {
            return 'Durum uygun degil';
        }

        return null;
    }

    getRoomChangeDisabledMessage(kayit: RezervasyonListeDto): string {
        const reason = this.getRoomChangeDisabledReason(kayit);
        if (reason === 'Aktif ihtiyac yok') {
            return 'Bu rezervasyon icin aktif oda degisimi ihtiyaci bulunmuyor.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda oda degisimi yapilamaz.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda oda degisimi yapilamaz.';
        }

        if (reason === 'Durum uygun degil') {
            return 'Oda degisimi yalnizca Taslak, Onayli veya Check-in Tamamlandi durumlarindaki rezervasyonlarda yapilabilir.';
        }

        if (reason === 'Yonetim yetkisi yok') {
            return 'Bu islem icin yetkiniz bulunmuyor.';
        }

        return 'Oda degisimi islemi su anda acilamaz.';
    }

    hasAnyRowAction(kayit: RezervasyonListeDto): boolean {
        return this.getRowActions(kayit).length > 0;
    }

    getRowActionDisabledMessages(kayit: RezervasyonListeDto): string[] {
        const messages: string[] = [];
        const paymentReason = this.getPaymentDisabledReason(kayit);
        if (paymentReason) {
            messages.push(`Odeme: ${this.getPaymentDisabledMessage(kayit)}`);
        }

        const roomChangeReason = this.getRoomChangeDisabledReason(kayit);
        if (roomChangeReason) {
            messages.push(`Oda degistir: ${this.getRoomChangeDisabledMessage(kayit)}`);
        }

        const checkInReason = this.getCheckInDisabledReason(kayit);
        if (checkInReason) {
            messages.push(`Check-in: ${this.getCheckInDisabledMessage(kayit)}`);
        }

        const checkOutReason = this.getCheckOutDisabledReason(kayit);
        if (checkOutReason) {
            messages.push(`Check-out: ${this.getCheckOutDisabledMessage(kayit)}`);
        }

        const cancelReason = this.getCancelReservationDisabledReason(kayit);
        if (cancelReason) {
            messages.push(`Iptal: ${this.getCancelReservationDisabledMessage(kayit)}`);
        }

        return messages;
    }

    getRowActionDisabledTooltip(kayit: RezervasyonListeDto): string {
        return this.getRowActionDisabledMessages(kayit).join(' | ');
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

        const roomChangeDisabledReason = this.getRoomChangeDisabledReason(kayit);
        items.push({
            label: roomChangeDisabledReason ? `Oda Degistir (${roomChangeDisabledReason})` : 'Oda Degistir',
            icon: 'pi pi-sync',
            disabled: roomChangeDisabledReason !== null,
            command: () => this.openOdaDegisimDialog(kayit)
        });

        const checkInDisabledReason = this.getCheckInDisabledReason(kayit);
        items.push({
            label: checkInDisabledReason ? `Check-in (${checkInDisabledReason})` : 'Check-in',
            icon: 'pi pi-arrow-right',
            disabled: checkInDisabledReason !== null,
            command: () => this.tamamlaCheckIn(kayit)
        });

        const checkOutDisabledReason = this.getCheckOutDisabledReason(kayit);
        items.push({
            label: checkOutDisabledReason ? `Check-out (${checkOutDisabledReason})` : 'Check-out',
            icon: 'pi pi-arrow-left',
            disabled: checkOutDisabledReason !== null,
            command: () => this.tamamlaCheckOut(kayit)
        });

        const paymentDisabledReason = this.getPaymentDisabledReason(kayit);
        items.push({
            label: paymentDisabledReason ? `Odeme (${paymentDisabledReason})` : 'Odeme',
            icon: 'pi pi-credit-card',
            disabled: paymentDisabledReason !== null,
            command: () => this.openOdemeDialog(kayit)
        });

        const cancelDisabledReason = this.getCancelReservationDisabledReason(kayit);
        const cancelActionLabel = kayit.rezervasyonDurumu === this.durumIptal
            ? 'Iptali Geri Al'
            : cancelDisabledReason
                ? `Iptal Et (${cancelDisabledReason})`
                : 'Iptal Et';

        items.push({
            label: cancelActionLabel,
            icon: 'pi pi-times',
            disabled: cancelDisabledReason !== null,
            command: () => this.iptalEt(kayit)
        });

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
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `Check-in tamamlandi. Referans: ${result.referansNo}`
                    });
                    this.openOdemeDialog(kayit);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private executeCheckInIfAllowed(kayit: RezervasyonListeDto, kontrol: RezervasyonCheckInKontrolDto): Observable<RezervasyonKayitSonucDto> {
        if (kontrol.uyarilar.length > 0) {
            const warningDetail = kontrol.uyarilar
                .map((x) => x.odaNo ? `${x.odaNo} - ${x.binaAdi} (${x.temizlikDurumu})` : x.mesaj)
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
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: result.rezervasyonDurumu === this.durumIptal
                            ? `Rezervasyon iptal edildi. Referans: ${result.referansNo}`
                            : `Rezervasyon iptali geri alindi. Referans: ${result.referansNo}`
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
        this.odaDegisimRezervasyonDurumu = kayit.rezervasyonDurumu;
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
        this.odaDegisimRezervasyonDurumu = null;
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
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `Oda degisimi kaydedildi. Referans: ${result.referansNo}`
                    });

                    const selectedTesis = this.selectedTesisId && this.selectedTesisId > 0 ? this.selectedTesisId : null;
                    this.loadRezervasyonKayitlari(selectedTesis);
                    this.closeOdaDegisimDialog();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getOdaDegisimAdayOptions(kayit: RezervasyonOdaDegisimKayitDto): { label: string; value: number }[] {
        return kayit.adayOdalar.map((aday) => ({
            value: aday.odaId,
            label: `${aday.odaNo} - ${aday.binaAdi} (${aday.odaTipiAdi}, kalan ${aday.kalanKapasite}${aday.paylasimliMi && aday.onerilenYatakNolari.length > 0 ? `, yatak ${aday.onerilenYatakNolari.join(', ')}` : ''})`
        }));
    }

    getSelectedOdaDegisimAday(kayit: RezervasyonOdaDegisimKayitDto): RezervasyonOdaDegisimAdayOdaDto | null {
        const seciliOdaId = this.getOdaDegisimSeciliOdaId(kayit);
        if (seciliOdaId <= 0) {
            return null;
        }

        return kayit.adayOdalar.find((x) => x.odaId === seciliOdaId) ?? null;
    }

    getOdaDegisimKonaklayanOnerileri(
        kayit: RezervasyonOdaDegisimKayitDto,
        aday: RezervasyonOdaDegisimAdayOdaDto | null
    ): Array<{ konaklayan: RezervasyonOdaDegisimKonaklayanDto; onerilenYatakNo: number | null }> {
        if (!aday) {
            return [];
        }

        return kayit.tasinacakKonaklayanlar.map((konaklayan, index) => ({
            konaklayan,
            onerilenYatakNo: aday.paylasimliMi ? (aday.onerilenYatakNolari[index] ?? null) : null
        }));
    }

    getTasinacakKonaklayanAdlari(kayit: RezervasyonOdaDegisimKayitDto): string {
        return kayit.tasinacakKonaklayanlar.map((x) => x.adSoyad).join(', ');
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
        this.odemeEkHizmetPanelExpanded = false;
        this.odemeTutari = null;
        this.odemeTipi = 'Nakit';
        this.odemeAciklama = '';
        this.odemeEkHizmetSecenekleri = null;
        this.selectedEkHizmetKonaklayanId = null;
        this.selectedEkHizmetTarifeId = null;
        this.ekHizmetTarihi = this.getDefaultEkHizmetTarihi(kayit.id);
        this.ekHizmetMiktar = 1;
        this.ekHizmetBirimFiyat = null;
        this.ekHizmetAciklama = '';
        this.editingEkHizmetId = null;
        this.loadOdemeOzeti(kayit.id);
        this.loadEkHizmetSecenekleri(kayit.id);
    }

    closeOdemeDialog(): void {
        this.odemeDialogVisible = false;
        this.odemeLoading = false;
        this.odemeSaving = false;
        this.ekHizmetSaving = false;
        this.odemeRezervasyonId = null;
        this.odemeReferansNo = '';
        this.odemeOzeti = null;
        this.odemeEkHizmetPanelExpanded = false;
        this.odemeEkHizmetSecenekleri = null;
        this.selectedEkHizmetKonaklayanId = null;
        this.selectedEkHizmetTarifeId = null;
        this.ekHizmetTarihi = '';
        this.ekHizmetMiktar = 1;
        this.ekHizmetBirimFiyat = null;
        this.ekHizmetAciklama = '';
        this.editingEkHizmetId = null;
        this.odemeTutari = null;
        this.odemeTipi = 'Nakit';
        this.odemeAciklama = '';
    }

    kaydetEkHizmet(): void {
        if (!this.odemeRezervasyonId || !this.odemeOzeti || this.ekHizmetSaving) {
            return;
        }

        if (this.getSelectedOdemeRezervasyonDurumu() !== this.durumCheckInTamamlandi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Check-in Bekleniyor', detail: 'Ek hizmet eklemek icin once check-in tamamlanmalidir.' });
            return;
        }

        if (!this.selectedEkHizmetKonaklayanId || this.selectedEkHizmetKonaklayanId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir konaklayan seciniz.' });
            return;
        }

        if (!this.selectedEkHizmetTarifeId || this.selectedEkHizmetTarifeId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir ek hizmet seciniz.' });
            return;
        }

        const miktar = Number(this.ekHizmetMiktar ?? 0);
        if (!Number.isFinite(miktar) || miktar <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Miktar', detail: 'Miktar sifirdan buyuk olmalidir.' });
            return;
        }

        const birimFiyat = Number(this.ekHizmetBirimFiyat ?? 0);
        if (!Number.isFinite(birimFiyat) || birimFiyat < 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Fiyat', detail: 'Birim fiyat sifirdan kucuk olamaz.' });
            return;
        }

        if (!this.ekHizmetTarihi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Hizmet tarihi zorunludur.' });
            return;
        }

        const request = {
            rezervasyonKonaklayanId: this.selectedEkHizmetKonaklayanId,
            ekHizmetTarifeId: this.selectedEkHizmetTarifeId,
            hizmetTarihi: this.toIsoDate(this.ekHizmetTarihi),
            miktar,
            birimFiyat,
            aciklama: this.normalizeOptional(this.ekHizmetAciklama)
        };
        const paketUyarisi = this.getSelectedEkHizmetTarifePaketUyarisi();
        const paketCakismaPolitikasi = this.odemeEkHizmetSecenekleri?.paketCakismaPolitikasi ?? EkHizmetPaketCakismaPolitikalari.OnayIste;

        if (paketUyarisi) {
            if (paketCakismaPolitikasi === EkHizmetPaketCakismaPolitikalari.Engelle) {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Paket Cakismasi', detail: `${paketUyarisi} Bu tesis politikasina gore ek satis engellendi.` });
                return;
            }

            if (paketCakismaPolitikasi === EkHizmetPaketCakismaPolitikalari.OnayIste) {
                this.confirmationService.confirm({
                    header: 'Paket Icerigi Uyarisi',
                    message: `${paketUyarisi} Yine de ek hizmet satisina devam etmek istiyor musunuz?`,
                    icon: 'pi pi-exclamation-triangle',
                    acceptLabel: 'Devam Et',
                    rejectLabel: 'Vazgec',
                    accept: () => this.executeKaydetEkHizmet(request)
                });
                return;
            }

            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Paket Cakismasi', detail: paketUyarisi });
        }

        this.executeKaydetEkHizmet(request);
    }

    private executeKaydetEkHizmet(request: {
        rezervasyonKonaklayanId: number;
        ekHizmetTarifeId: number;
        hizmetTarihi: string;
        miktar: number;
        birimFiyat: number;
        aciklama: string | null;
    }): void {
        if (!this.odemeRezervasyonId) {
            return;
        }

        const rezervasyonId = this.odemeRezervasyonId;
        this.ekHizmetSaving = true;

        const request$ = this.editingEkHizmetId
            ? this.service.guncelleEkHizmet(rezervasyonId, this.editingEkHizmetId, request)
            : this.service.kaydetEkHizmet(rezervasyonId, request);

        request$
            .pipe(
                finalize(() => {
                    this.ekHizmetSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.applyOdemeOzetiToRezervasyon(result);
                    this.odemeTutari = result.kalanTutar > 0 ? result.kalanTutar : null;
                    const message = this.editingEkHizmetId
                        ? 'Ek hizmet kaydi guncellendi.'
                        : 'Ek hizmet hesaba eklendi.';
                    this.resetEkHizmetForm();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: message });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    duzenleEkHizmet(hizmet: RezervasyonEkHizmetDto): void {
        if (this.getEkHizmetEditDisabledReason(hizmet) !== null) {
            return;
        }

        this.odemeEkHizmetPanelExpanded = true;
        this.editingEkHizmetId = hizmet.id;
        this.selectedEkHizmetKonaklayanId = hizmet.rezervasyonKonaklayanId;
        this.selectedEkHizmetTarifeId = hizmet.ekHizmetTarifeId;
        this.ekHizmetTarihi = this.toDateTimeLocalInputValue(hizmet.hizmetTarihi);
        this.ekHizmetMiktar = hizmet.miktar;
        this.ekHizmetBirimFiyat = hizmet.birimFiyat;
        this.ekHizmetAciklama = hizmet.aciklama ?? '';
    }

    onEkHizmetTarifeChange(): void {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        this.ekHizmetBirimFiyat = selectedTarife?.birimFiyat ?? null;
    }

    toggleOdemeEkHizmetPanel(): void {
        this.odemeEkHizmetPanelExpanded = !this.odemeEkHizmetPanelExpanded;
    }

    ekHizmetDuzenlemeyiIptalEt(): void {
        this.resetEkHizmetForm();
    }

    silEkHizmet(hizmet: RezervasyonEkHizmetDto): void {
        if (!this.odemeRezervasyonId || this.getEkHizmetDeleteDisabledReason(hizmet) !== null || this.ekHizmetSaving) {
            return;
        }

        if (!window.confirm(`'${hizmet.tarifeAdi}' ek hizmet kaydi silinsin mi?`)) {
            return;
        }

        this.ekHizmetSaving = true;
        this.service
            .silEkHizmet(this.odemeRezervasyonId, hizmet.id)
            .pipe(
                finalize(() => {
                    this.ekHizmetSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.applyOdemeOzetiToRezervasyon(result);
                    this.odemeTutari = result.kalanTutar > 0 ? result.kalanTutar : null;
                    if (this.editingEkHizmetId === hizmet.id) {
                        this.resetEkHizmetForm();
                    }

                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Ek hizmet kaydi silindi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    kaydetOdeme(): void {
        if (!this.odemeRezervasyonId || !this.odemeOzeti || this.odemeSaving) {
            return;
        }

        if (this.getSelectedOdemeRezervasyonDurumu() !== this.durumCheckInTamamlandi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Check-in Bekleniyor', detail: 'Odeme almak icin once check-in tamamlanmalidir.' });
            return;
        }

        const tutar = Number(this.odemeTutari ?? 0);
        if (!Number.isFinite(tutar) || tutar <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Tutar', detail: 'Odeme tutari sifirdan buyuk olmalidir.' });
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
                    this.applyOdemeOzetiToRezervasyon(result);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Odeme kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `Check-out tamamlandi. Referans: ${result.referansNo}`
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getKonaklayanSegmentler(): RezervasyonKonaklayanSegmentDto[] {
        return this.konaklayanPlan?.segmentler ?? [];
    }

    getKonaklayanOdaSecenekleri(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): { label: string; value: number }[] {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return [];
        }

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
                label: `${oda.odaNo} - ${oda.binaAdi} (${oda.odaTipiAdi}, ${oda.ayrilanKisiSayisi} kisi, ${oda.paylasimliMi ? 'paylasimli' : 'paylasimsiz'})`
            }));
    }

    getKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): number | null {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        return atama?.odaId ?? null;
    }

    getKonaklayanAtamaYatakNo(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): number | null {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        return atama?.yatakNo ?? null;
    }

    isKonaklayanYatakSecimiGerekli(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): boolean {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return false;
        }

        const odaId = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        if (!odaId || odaId <= 0) {
            return false;
        }

        const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
        return !!odaSecenegi?.paylasimliMi;
    }

    getKonaklayanYatakSecenekleri(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): { label: string; value: number }[] {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return [];
        }

        const odaId = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        if (!odaId || odaId <= 0) {
            return [];
        }

        const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
        if (!odaSecenegi || !odaSecenegi.paylasimliMi || odaSecenegi.ayrilanKisiSayisi <= 0) {
            return [];
        }

        const currentYatakNo = this.getKonaklayanAtamaYatakNo(kisi, segmentId);
        const selectedBeds = this.getSegmentOdaSelectedBeds(segmentId, odaId);
        const options: { label: string; value: number }[] = [];
        for (let bedNo = 1; bedNo <= odaSecenegi.ayrilanKisiSayisi; bedNo++) {
            if (currentYatakNo === bedNo || !selectedBeds.has(bedNo)) {
                options.push({ label: `Yatak ${bedNo}`, value: bedNo });
            }
        }

        return options;
    }

    setKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number, odaId: number | null): void {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        if (atama) {
            atama.odaId = odaId;
            const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
            if (!odaSecenegi || !odaSecenegi.paylasimliMi) {
                atama.yatakNo = null;
                return;
            }

            if ((atama.yatakNo ?? 0) <= 0 || (atama.yatakNo ?? 0) > odaSecenegi.ayrilanKisiSayisi) {
                atama.yatakNo = null;
            }
            return;
        }

        kisi.atamalar = [...kisi.atamalar, { segmentId, odaId, yatakNo: null }];
    }

    setKonaklayanAtamaYatakNo(kisi: RezervasyonKonaklayanKisiDto, segmentId: number, yatakNo: number | null): void {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        if (atama) {
            atama.yatakNo = yatakNo;
            return;
        }

        kisi.atamalar = [...kisi.atamalar, { segmentId, odaId: null, yatakNo }];
    }

    private getSegmentOdaSelectedCount(segmentId: number, odaId: number): number {
        if (!this.konaklayanPlan) {
            return 0;
        }

        return this.konaklayanPlan.konaklayanlar.reduce((total, kisi) => {
            if (!this.isKonaklayanAssignmentsRequired(kisi)) {
                return total;
            }

            const selectedOdaId = kisi.atamalar.find((x) => x.segmentId === segmentId)?.odaId;
            return total + (selectedOdaId === odaId ? 1 : 0);
        }, 0);
    }

    private getSegmentOdaSelectedBeds(segmentId: number, odaId: number): Set<number> {
        if (!this.konaklayanPlan) {
            return new Set<number>();
        }

        const selected = this.konaklayanPlan.konaklayanlar
            .filter((kisi) => this.isKonaklayanAssignmentsRequired(kisi))
            .map((kisi) => kisi.atamalar.find((x) => x.segmentId === segmentId))
            .filter((atama) => atama?.odaId === odaId && (atama?.yatakNo ?? 0) > 0)
            .map((atama) => atama!.yatakNo!) as number[];
        return new Set(selected);
    }

    private getKonaklayanSegmentOdaSecenegi(segmentId: number, odaId: number | null | undefined): RezervasyonKonaklayanOdaSecenekDto | null {
        if (!odaId || odaId <= 0 || !this.konaklayanPlan) {
            return null;
        }

        const segment = this.konaklayanPlan.segmentler.find((x) => x.segmentId === segmentId);
        if (!segment) {
            return null;
        }

        return segment.odaSecenekleri.find((x) => x.odaId === odaId) ?? null;
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
            misafirTipleri: this.service.getMisafirTipleri()
        })
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, misafirTipleri }) => {
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.misafirTipleri = [...misafirTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));

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

                    if (this.selectedTesisId && this.selectedTesisId > 0) {
                        this.applySelectedTesisDateTimes();
                        this.loadOdaTipleri(this.selectedTesisId);
                        this.loadKonaklamaTipleriByTesis(this.selectedTesisId);
                        this.loadRezervasyonKayitlari(this.selectedTesisId);
                        return;
                    }

                    this.odaTipleri = [];
                    this.konaklamaTipleri = [];
                    this.selectedOdaTipiId = null;
                    this.selectedKonaklamaTipiId = null;
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadKonaklamaTipleriByTesis(tesisId: number): void {
        this.service.getKonaklamaTipleri(tesisId).subscribe({
            next: (konaklamaTipleri) => {
                this.konaklamaTipleri = [...konaklamaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));

                if (this.selectedKonaklamaTipiId && !this.konaklamaTipleri.some((x) => x.id === this.selectedKonaklamaTipiId)) {
                    this.selectedKonaklamaTipiId = null;
                }

                if (!this.selectedKonaklamaTipiId && this.konaklamaTipleri.length > 0) {
                    this.selectedKonaklamaTipiId = this.konaklamaTipleri[0].id;
                }

                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.konaklamaTipleri = [];
                this.selectedKonaklamaTipiId = null;
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    if (ozet.odemeler.length > 0 && this.editingEkHizmetId) {
                        this.resetEkHizmetForm();
                    }

                    this.applyOdemeOzetiToRezervasyon(ozet);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.odemeOzeti = null;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadEkHizmetSecenekleri(rezervasyonId: number): void {
        this.service.getEkHizmetSecenekleri(rezervasyonId).subscribe({
            next: (result) => {
                this.odemeEkHizmetSecenekleri = result;
                if (!this.selectedEkHizmetKonaklayanId || !result.misafirler.some((x) => x.rezervasyonKonaklayanId === this.selectedEkHizmetKonaklayanId)) {
                    this.selectedEkHizmetKonaklayanId = result.misafirler[0]?.rezervasyonKonaklayanId ?? null;
                }

                if (!this.selectedEkHizmetTarifeId || !result.tarifeler.some((x) => x.id === this.selectedEkHizmetTarifeId)) {
                    this.selectedEkHizmetTarifeId = result.tarifeler[0]?.id ?? null;
                }

                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.odemeEkHizmetSecenekleri = null;
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                this.cdr.detectChanges();
            }
        });
    }

    private applyOdemeOzetiToRezervasyon(ozet: RezervasyonOdemeOzetDto): void {
        const kayit = this.rezervasyonKayitlari.find((x) => x.id === ozet.rezervasyonId);
        if (kayit) {
            kayit.toplamUcret = ozet.toplamUcret;
            kayit.odenenTutar = ozet.odenenTutar;
            kayit.kalanTutar = ozet.kalanTutar;
        }

        const detay = this.rezervasyonDetayById[ozet.rezervasyonId];
        if (detay) {
            detay.konaklamaUcreti = ozet.konaklamaUcreti;
            detay.ekHizmetToplami = ozet.ekHizmetToplami;
            detay.toplamUcret = ozet.toplamUcret;
            detay.ekHizmetler = [...ozet.ekHizmetler];
        }
    }

    private getSelectedEkHizmetTarife(): RezervasyonEkHizmetTarifeSecenekDto | null {
        if (!this.selectedEkHizmetTarifeId) {
            return null;
        }

        return this.ekHizmetTarifeSecenekleri.find((x) => x.id === this.selectedEkHizmetTarifeId) ?? null;
    }

    getSelectedEkHizmetTarifeVarsayilanFiyat(): string | null {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        if (!selectedTarife) {
            return null;
        }

        return this.formatCurrency(selectedTarife.birimFiyat, selectedTarife.paraBirimi);
    }

    getSelectedEkHizmetTarifePaketUyarisi(): string | null {
        return this.getSelectedEkHizmetTarife()?.paketIcerigiUyariMesaji ?? null;
    }

    getEkHizmetToplamOnizleme(): string | null {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        const miktar = Number(this.ekHizmetMiktar ?? 0);
        const birimFiyat = Number(this.ekHizmetBirimFiyat ?? 0);
        if (!selectedTarife || !Number.isFinite(miktar) || miktar <= 0 || !Number.isFinite(birimFiyat) || birimFiyat < 0) {
            return null;
        }

        return this.formatCurrency(Math.round(birimFiyat * miktar * 100) / 100, selectedTarife.paraBirimi);
    }

    private getDefaultEkHizmetTarihi(rezervasyonId: number): string {
        const kayit = this.rezervasyonKayitlari.find((x) => x.id === rezervasyonId);
        if (!kayit) {
            return this.nowInput();
        }

        const now = new Date();
        const giris = new Date(kayit.girisTarihi);
        const cikis = new Date(kayit.cikisTarihi);
        const value = now >= giris && now <= cikis ? now : giris;
        return new Date(value.getTime() - value.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    }

    private getSelectedOdemeRezervasyonDurumu(): string | null {
        if (!this.odemeRezervasyonId) {
            return null;
        }

        return this.rezervasyonKayitlari.find((x) => x.id === this.odemeRezervasyonId)?.rezervasyonDurumu ?? null;
    }

    private getEkHizmetModificationDisabledReason(): string | null {
        if (!this.odemeOzeti) {
            return 'Odeme ozeti yok';
        }

        const durum = this.getSelectedOdemeRezervasyonDurumu();
        if (durum !== this.durumCheckInTamamlandi) {
            if (durum === this.durumCheckOutTamamlandi) {
                return 'Check-out tamamlandi';
            }

            if (durum === this.durumIptal) {
                return 'Iptal edildi';
            }

            return 'Check-in bekleniyor';
        }

        return null;
    }

    getEkHizmetEditDisabledReason(_hizmet: RezervasyonEkHizmetDto): string | null {
        return this.getEkHizmetModificationDisabledReason();
    }

    getEkHizmetEditDisabledMessage(hizmet: RezervasyonEkHizmetDto): string {
        const reason = this.getEkHizmetEditDisabledReason(hizmet);
        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Check-in bekleniyor') {
            return 'Check-in tamamlanmadan ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Odeme ozeti yok') {
            return 'Ek hizmet islemi icin odeme ozeti yuklenemedi.';
        }

        return 'Ek hizmet kalemi guncellenemez.';
    }

    getEkHizmetDeleteDisabledReason(hizmet: RezervasyonEkHizmetDto): string | null {
        const reservationReason = this.getEkHizmetModificationDisabledReason();
        if (reservationReason) {
            return reservationReason;
        }

        if (!this.odemeOzeti) {
            return 'Odeme ozeti yok';
        }

        const kalanTutar = this.odemeOzeti.kalanTutar ?? 0;
        if (kalanTutar <= 0) {
            return 'Odeme bakiyesi sifirlandi';
        }

        if (hizmet.toplamTutar > kalanTutar) {
            return 'Fazla odeme olusur';
        }

        return null;
    }

    getEkHizmetDeleteDisabledMessage(hizmet: RezervasyonEkHizmetDto): string {
        const reason = this.getEkHizmetDeleteDisabledReason(hizmet);
        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemi silinemez.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemi silinemez.';
        }

        if (reason === 'Check-in bekleniyor') {
            return 'Check-in tamamlanmadan ek hizmet kalemi silinemez.';
        }

        if (reason === 'Odeme bakiyesi sifirlandi') {
            return 'Odeme bakiyesi sifirlandigi icin bu ek hizmet kalemi silinemez.';
        }

        if (reason === 'Fazla odeme olusur') {
            return `Bu kalem silinirse tahsil edilen tutar yeni toplamdan buyuk kalir. Kalan tutar: ${this.formatCurrency(this.odemeOzeti?.kalanTutar ?? 0, this.odemeOzeti?.paraBirimi ?? 'TRY')}.`;
        }

        if (reason === 'Odeme ozeti yok') {
            return 'Ek hizmet islemi icin odeme ozeti yuklenemedi.';
        }

        return 'Ek hizmet kalemi silinemez.';
    }

    private resetEkHizmetForm(): void {
        this.editingEkHizmetId = null;
        this.ekHizmetMiktar = 1;
        this.ekHizmetBirimFiyat = this.getSelectedEkHizmetTarife()?.birimFiyat ?? null;
        this.ekHizmetAciklama = '';

        if (this.odemeRezervasyonId) {
            this.ekHizmetTarihi = this.getDefaultEkHizmetTarihi(this.odemeRezervasyonId);
        }
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
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

    getKonaklamaIcerikKuralOzeti(icerik: {
        kullanimTipiAdi?: string | null;
        kullanimNoktasiAdi?: string | null;
        kullanimBaslangicSaati?: string | null;
        kullanimBitisSaati?: string | null;
        checkInGunuGecerliMi?: boolean;
        checkOutGunuGecerliMi?: boolean;
    }): string {
        const parcali: string[] = [];

        if (icerik.kullanimTipiAdi) {
            parcali.push(icerik.kullanimTipiAdi);
        }

        if (icerik.kullanimNoktasiAdi) {
            parcali.push(icerik.kullanimNoktasiAdi);
        }

        if (icerik.kullanimBaslangicSaati && icerik.kullanimBitisSaati) {
            parcali.push(`${icerik.kullanimBaslangicSaati}-${icerik.kullanimBitisSaati}`);
        }

        const gunKurali: string[] = [];
        if (icerik.checkInGunuGecerliMi === false) {
            gunKurali.push('check-in gunu yok');
        }
        if (icerik.checkOutGunuGecerliMi === false) {
            gunKurali.push('check-out gunu yok');
        }
        if (gunKurali.length > 0) {
            parcali.push(gunKurali.join(', '));
        }

        return parcali.join(' • ');
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
                return UiSeverity.Secondary;
            case this.durumOnayli:
                return UiSeverity.Info;
            case this.durumCheckInTamamlandi:
                return UiSeverity.Warn;
            case this.durumCheckOutTamamlandi:
                return UiSeverity.Success;
            case this.durumIptal:
                return UiSeverity.Danger;
            default:
                return UiSeverity.Secondary;
        }
    }

    getKonaklayanKatilimChipleri(kayit: RezervasyonListeDto): Array<{ label: string; severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' }> {
        const gelen = Math.max(0, kayit.gelenKonaklayanSayisi ?? 0);
        const bekleyen = Math.max(0, kayit.bekleyenKonaklayanSayisi ?? 0);
        const gelmeyen = Math.max(0, (kayit.kisiSayisi ?? 0) - gelen - bekleyen);
        const chips: Array<{ label: string; severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' }> = [];

        if (gelen > 0) {
            chips.push({ label: `Geldi: ${gelen}`, severity: UiSeverity.Success });
        }

        if (bekleyen > 0) {
            chips.push({ label: `Bekliyor: ${bekleyen}`, severity: UiSeverity.Warn });
        }

        if (gelmeyen > 0) {
            chips.push({ label: `Gelmedi: ${gelmeyen}`, severity: UiSeverity.Danger });
        }

        if (chips.length === 0) {
            chips.push({ label: 'Katilim bilgisi yok', severity: UiSeverity.Secondary });
        }

        return chips;
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

    private hasSharedRoomUsage(scenario: KonaklamaSenaryoDto): boolean {
        return scenario.segmentler.some((segment) => segment.odaAtamalari.some((assignment) => assignment.paylasimliMi));
    }

    private toDateTimeLocalInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}`;
    }

    private toDateTimeLocalInputValue(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return this.toDateTimeLocalInput(date);
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

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { RezervasyonDegisiklikGecmisiDialogComponent } from './components/rezervasyon-degisiklik-gecmisi-dialog/rezervasyon-degisiklik-gecmisi-dialog';
import { RezervasyonKonaklayanPlaniDialogComponent } from './components/rezervasyon-konaklayan-plani-dialog/rezervasyon-konaklayan-plani-dialog';
import { RezervasyonOdaDegisimiDialogComponent } from './components/rezervasyon-oda-degisimi-dialog/rezervasyon-oda-degisimi-dialog';
import { RezervasyonOdemeDialogComponent } from './components/rezervasyon-odeme-dialog/rezervasyon-odeme-dialog';
import { EMPTY, finalize, Observable, switchMap } from 'rxjs';
import { ConfirmationService, MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
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
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';
import { KonaklayanCinsiyetleri } from './konaklayan-cinsiyetleri.constants';
import {
    KonaklamaSenaryoDto,
    KonaklamaSenaryoOdaAtamaDto,
    KonaklamaSenaryoSegmentDto,
    UygunOdaDto,
    UygunOdaAramaRequestDto,
    RezervasyonAramaRequestDto,
    RezervasyonAramaSonucDto,
    RezervasyonCheckInKontrolDto,
    RezervasyonDetayDto,
    RezervasyonIndirimKuraliSecenekDto,
    RezervasyonKaydetRequestDto,
    RezervasyonKayitSonucDto,
    RezervasyonKonaklamaHakkiDto,
    RezervasyonKonaklamaHakkiTuketimKaydiDto,
    RezervasyonKonaklamaHakkiTuketimNoktasiDto,
    RezervasyonKonaklamaTipiDto,
    RezervasyonListeDto,
    RezervasyonMisafirTipiDto,
    RezervasyonOdemeOzetDto,
    RezervasyonOdaTipiDto,
    SenaryoFiyatHesaplamaSonucuDto
} from './rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from './rezervasyon-yonetimi.service';

type RezervasyonNavAction =
    | 'odeme'
    | 'odaDegisim'
    | 'konaklayanPlan'
    | 'degisiklikGecmisi';

@Component({
    selector: 'app-rezervasyon-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, CheckboxModule, ConfirmDialogModule, DatePickerModule, DialogModule, InputTextModule, MenuModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule, TooltipModule, RezervasyonDegisiklikGecmisiDialogComponent, RezervasyonKonaklayanPlaniDialogComponent, RezervasyonOdaDegisimiDialogComponent, RezervasyonOdemeDialogComponent],
    templateUrl: './rezervasyon-yonetimi.html',
    styleUrl: './rezervasyon-yonetimi.scss',
    providers: [MessageService, ConfirmationService]
})
export class RezervasyonYonetimi implements OnInit {
    protected readonly Math = Math;
    private readonly service = inject(RezervasyonYonetimiService);
    private readonly tesisService = inject(TesisYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly route = inject(ActivatedRoute);

    private pendingNavRezervasyonId: number | null = null;
    private pendingNavAction: RezervasyonNavAction | null = null;

    tesisler: TesisDto[] = [];
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
    originalScenarioForReservation: KonaklamaSenaryoDto | null = null;
    discountDialogVisible = false;
    rezervasyonUcretDetayDialogVisible = false;
    rezervasyonUcretDetayRezervasyonId: number | null = null;
    rezervasyonUcretDetayReferansNo = '';
    degisiklikGecmisiDialogVisible = false;
    degisiklikGecmisiRezervasyonId: number | null = null;
    degisiklikGecmisiReferansNo = '';
    konaklayanPlanDialogVisible = false;
    konaklayanPlanRezervasyonId: number | null = null;
    konaklayanPlanReferansNo = '';
    readonly konaklayanCinsiyetSecenekleri = [
        { label: 'Kadin', value: KonaklayanCinsiyetleri.Kadin },
        { label: 'Erkek', value: KonaklayanCinsiyetleri.Erkek }
    ];
    odemeDialogVisible = false;
    odemeRezervasyonId: number | null = null;
    odemeReferansNo = '';
    odemeRezervasyonDurumu: string | null = null;
    odemeTesisId: number | null = null;
    odemeMisafirAdiSoyadi = '';
    odemeMisafirTelefon = '';
    odemeMisafirTcKimlikNo: string | null = null;
    odaDegisimDialogVisible = false;
    odaDegisimRezervasyonId: number | null = null;
    odaDegisimReferansNo = '';
    odaDegisimRezervasyonDurumu: string | null = null;
    alternatifOdaDialogVisible = false;
    alternatifOdaLoading = false;
    alternatifOdalar: UygunOdaDto[] = [];
    selectedSenaryoForOdaDegistir: KonaklamaSenaryoDto | null = null;
    selectedSegmentForOdaDegistir: KonaklamaSenaryoSegmentDto | null = null;
    selectedAtamaForOdaDegistir: KonaklamaSenaryoOdaAtamaDto | null = null;
    rowActionItems: MenuItem[] = [];
    checkActionLoadingByRezervasyonId: Record<number, boolean> = {};

    selectedTesisId: number | null = null;
    selectedOdaTipiId: number | null = null;
    selectedMisafirTipiId: number | null = null;
    selectedKonaklamaTipiId: number | null = null;
    selectedRezervasyonDurumFiltre = 'Tum';
    kisiSayisi = 1;
    tekKisilikFiyatUygulansinMi = false;
    senaryoKonaklayanCinsiyetleri: Array<string | null> = [KonaklayanCinsiyetleri.Erkek];
    baslangicTarihi: Date | null = this.createDefaultCheckInDate();
    bitisTarihi: Date | null = this.createDefaultCheckOutDate();
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

    aramaMetni = '';
    aramaDurumu: string | null = null;
    aramaGirisBaslangic: Date | null = null;
    aramaGirisBitis: Date | null = null;
    aramaCikisBaslangic: Date | null = null;
    aramaCikisBitis: Date | null = null;
    aramaSadeceOdemesiKalanlar = false;
    aramaSadeceOdaDegisimiGerekli = false;
    aramaToplamKayitSayisi = 0;
    aramaPage = 1;
    aramaPageSize = 50;

    private readonly defaultGirisSaati = '14:00';
    private readonly defaultCikisSaati = '10:00';
    readonly rezervasyonDurumFiltreSecenekleri = [
        { label: 'Tum Kayitlar', value: 'Tum' },
        { label: 'Taslak', value: 'Taslak' },
        { label: 'Onayli', value: 'Onayli' },
        { label: 'Check-in Yapilmis', value: 'CheckInTamamlandi' },
        { label: 'Check-out Yapilmis', value: 'CheckOutTamamlandi' },
        { label: 'Iptal Edilmis', value: 'Iptal' }
    ];
    readonly aramaDurumSecenekleri = [
        { label: 'Tum Durumlar', value: null },
        { label: 'Taslak', value: 'Taslak' },
        { label: 'Onayli', value: 'Onayli' },
        { label: 'Check-in Tamamlandi', value: 'CheckInTamamlandi' },
        { label: 'Check-out Tamamlandi', value: 'CheckOutTamamlandi' },
        { label: 'Iptal', value: 'Iptal' }
    ];
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

    get selectedRezervasyonOdaTipi(): RezervasyonOdaTipiDto | null {
        return this.odaTipleri.find((x) => x.id === this.selectedOdaTipiId) ?? null;
    }

    get canUseSinglePersonPricing(): boolean {
        if (!this.selectedRezervasyonOdaTipi) {
            return true;
        }

        return !this.selectedRezervasyonOdaTipi.paylasimliMi;
    }

    get singlePersonPricingHint(): string | null {
        if (this.selectedRezervasyonOdaTipi?.paylasimliMi) {
            return 'Secili oda tipi paylasimli. Bu durumda sistem zaten kisi bazli fiyat uygular.';
        }

        if (this.kisiSayisi === 1) {
            return 'Bu secenek aciksa, paylasimsiz odada kisi bazli tarife kullanilir. Kapaliysa kapasite dolmamis odalarda ozel kullanim birim fiyatı kisi sayisina gore hesaplanir.';
        }

        return 'Bu secenek aciksa, paylasimsiz odada kisi bazli tarife rezervasyondaki her kisi icin uygulanir. Kapaliysa kapasite dolmamis odalarda ozel kullanim birim fiyatı kisi sayisina gore hesaplanir.';
    }

    get canApplyCustomDiscount(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.CustomIndirimGirebilir');
    }

    ngOnInit(): void {
        // Defensive cleanup: stale scroll-lock classes can remain after modal/sidebar flows.
        document.body.classList.remove('p-overflow-hidden');
        document.body.classList.remove('blocked-scroll');

        const params = this.route.snapshot.queryParams;
        const idParam = params['rezervasyonId'];
        if (idParam) {
            const parsed = Number(idParam);
            if (!isNaN(parsed) && parsed > 0) {
                this.pendingNavRezervasyonId = parsed;
                this.pendingNavAction = this.parseRezervasyonNavAction(params['action']);
            }
        }

        this.loadReferences();
    }

    refresh(): void {
        this.loadReferences();
    }

    private parseRezervasyonNavAction(value: unknown): RezervasyonNavAction | null {
        switch (value) {
            case 'odeme':
            case 'odaDegisim':
            case 'konaklayanPlan':
            case 'degisiklikGecmisi':
                return value;
            default:
                return null;
        }
    }

    private handlePendingNavAction(): void {
        const rezervasyonId = this.pendingNavRezervasyonId;
        const action = this.pendingNavAction;
        if (!rezervasyonId) return;

        this.pendingNavRezervasyonId = null;
        this.pendingNavAction = null;

        const kayit = this.rezervasyonKayitlari.find(k => k.id === rezervasyonId);
        if (!kayit) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Uyarı',
                detail: `Rezervasyon kaydı listede bulunamadı. Rezervasyon No: ${rezervasyonId}. Lütfen filtreleri kontrol edin.`
            });
            return;
        }

        switch (action) {
            case 'odeme':
                this.openOdemeDialog(kayit);
                break;
            case 'odaDegisim':
                this.openOdaDegisimDialog(kayit);
                break;
            case 'konaklayanPlan':
                this.openKonaklayanPlaniDialog(kayit);
                break;
            case 'degisiklikGecmisi':
                this.openDegisiklikGecmisiDialog(kayit);
                break;
            default:
                this.loadRezervasyonDetay(rezervasyonId);
                this.expandedRowKeys = { [rezervasyonId]: true };
        }
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
            this.misafirTipleri = [];
            this.konaklamaTipleri = [];
            this.selectedOdaTipiId = null;
            this.selectedMisafirTipiId = null;
            this.selectedKonaklamaTipiId = null;
            this.senaryolar = [];
            this.rezervasyonKayitlari = [];
            this.availableDiscountRules = [];
            return;
        }

        this.applySelectedTesisDateTimes();
        this.loadOdaTipleri(this.selectedTesisId, true);
        this.loadMisafirTipleriByTesis(this.selectedTesisId);
        this.loadKonaklamaTipleriByTesis(this.selectedTesisId);
        this.araRezervasyonlar(1);
    }

    onOdaTipiSelectionChange(): void {
        if (!this.canUseSinglePersonPricing) {
            this.tekKisilikFiyatUygulansinMi = false;
        }
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

        if (!this.baslangicTarihi || !this.bitisTarihi || this.baslangicTarihi.getTime() >= this.bitisTarihi.getTime()) {
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
                baslangicTarihi: this.toLocalDateTimeString(this.baslangicTarihi),
                bitisTarihi: this.toLocalDateTimeString(this.bitisTarihi),
                tekKisilikFiyatUygulansinMi: this.tekKisilikFiyatUygulansinMi,
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
        if (!this.canUseSinglePersonPricing) {
            this.tekKisilikFiyatUygulansinMi = false;
        }
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

    getScenarioPricingLabel(scenario: KonaklamaSenaryoDto): string {
        const pricingType = scenario.fiyatlamaTipi?.trim().toLowerCase().replace(/[^a-z0-9]/g, '');
        if (pricingType) {
            switch (pricingType) {
                case 'tekkisilikfiyat':
                    return 'Tek Kisilik Fiyat';
                case 'kisibasi':
                    return 'Kisi Basi';
                case 'ozelkullanim':
                    return 'Ozel Kullanim';
                case 'karma':
                    return 'Karma';
            }
        }

        if (this.tekKisilikFiyatUygulansinMi && this.kisiSayisi === 1) {
            return 'Tek Kisilik Fiyat';
        }

        const hasShared = this.hasSharedRoomUsage(scenario);
        const hasPrivate = scenario.segmentler.some((segment) => segment.odaAtamalari.some((assignment) => !assignment.paylasimliMi));

        if (hasShared && hasPrivate) {
            return 'Karma';
        }

        if (hasShared) {
            return 'Kisi Basi';
        }

        return 'Ozel Kullanim';
    }

    getScenarioPricingSeverity(scenario: KonaklamaSenaryoDto): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (this.getScenarioPricingLabel(scenario)) {
            case 'Tek Kisilik Fiyat':
                return UiSeverity.Info;
            case 'Kisi Basi':
                return UiSeverity.Info;
            case 'Karma':
                return UiSeverity.Warn;
            case 'Ozel Kullanim':
                return UiSeverity.Success;
            default:
                return UiSeverity.Secondary;
        }
    }

    private syncSenaryoKonaklayanCinsiyetleri(): void {
        const targetCount = Math.max(1, Math.trunc(this.kisiSayisi || 1));
        if (this.senaryoKonaklayanCinsiyetleri.length === targetCount) {
            if (!this.misafirCinsiyeti && this.senaryoKonaklayanCinsiyetleri.length > 0) {
                this.misafirCinsiyeti = this.senaryoKonaklayanCinsiyetleri[0];
            }
            return;
        }

        this.senaryoKonaklayanCinsiyetleri = Array.from({ length: targetCount }, (_, index) => this.senaryoKonaklayanCinsiyetleri[index] ?? KonaklayanCinsiyetleri.Erkek);
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
            girisTarihi: this.toLocalDateTimeString(this.baslangicTarihi),
            cikisTarihi: this.toLocalDateTimeString(this.bitisTarihi),
            tekKisilikFiyatUygulansinMi: this.tekKisilikFiyatUygulansinMi,
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
                    this.araRezervasyonlar(1);
                    this.search();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    openAlternatifOdaDialog(senaryo: KonaklamaSenaryoDto, segment: KonaklamaSenaryoSegmentDto, atama: KonaklamaSenaryoOdaAtamaDto): void {
        if (!this.selectedTesisId) {
            return;
        }
        this.selectedSenaryoForOdaDegistir = senaryo;
        this.selectedSegmentForOdaDegistir = segment;
        this.selectedAtamaForOdaDegistir = atama;
        this.alternatifOdalar = [];
        this.alternatifOdaDialogVisible = true;
        this.alternatifOdaLoading = true;

        const request: UygunOdaAramaRequestDto = {
            tesisId: this.selectedTesisId,
            odaTipiId: atama.odaTipiId,
            kisiSayisi: atama.ayrilanKisiSayisi,
            baslangicTarihi: segment.baslangicTarihi,
            bitisTarihi: segment.bitisTarihi
        };

        this.service
            .searchUygunOdalar(request)
            .pipe(finalize(() => { this.alternatifOdaLoading = false; this.cdr.detectChanges(); }))
            .subscribe({
                next: (odalar) => {
                    this.alternatifOdalar = odalar.filter(o => o.odaId !== atama.odaId);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    selectAlternatifOda(oda: UygunOdaDto): void {
        const atama = this.selectedAtamaForOdaDegistir;
        if (!atama) {
            return;
        }
        atama.odaId = oda.odaId;
        atama.odaNo = oda.odaNo;
        atama.binaId = oda.binaId;
        atama.binaAdi = oda.binaAdi;
        atama.odaTipiId = oda.odaTipiId;
        atama.odaTipiAdi = oda.odaTipiAdi;
        atama.kapasite = oda.kapasite;
        atama.paylasimliMi = oda.paylasimliMi;
        this.alternatifOdaDialogVisible = false;
        this.messageService.add({ severity: UiSeverity.Success, summary: 'Guncellendi', detail: 'Oda secimi guncellendi. Fiyat bilgisi degismis olabilir; rezervasyon onayinda tekrar hesaplanacaktir.' });
        this.cdr.detectChanges();
    }

    openDiscountDialog(scenario: KonaklamaSenaryoDto): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (!this.selectedTesisId || !this.selectedMisafirTipiId || !this.selectedKonaklamaTipiId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tesis, misafir tipi ve konaklama tipi secimi zorunludur.' });
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

        const mevcutIndirimler = [...(scenario.uygulananIndirimler ?? [])];
        this.originalScenarioForReservation = { ...scenario };
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
                this.toLocalDateTimeString(this.baslangicTarihi),
                this.toLocalDateTimeString(this.bitisTarihi)
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
                kisiSayisi: this.kisiSayisi,
                baslangicTarihi: this.toLocalDateTimeString(this.baslangicTarihi),
                bitisTarihi: this.toLocalDateTimeString(this.bitisTarihi),
                tekKisilikFiyatUygulansinMi: this.tekKisilikFiyatUygulansinMi,
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
        this.originalScenarioForReservation = null;
        this.availableDiscountRules = [];
        this.selectedDiscountRuleIds = [];
        this.customDiscountAmount = null;
        this.customDiscountDescription = '';
        this.scenarioPriceBreakdown = null;
    }

    reserveWithoutDiscounts(): void {
        if (!this.originalScenarioForReservation || this.saving) {
            return;
        }

        const scenario = this.originalScenarioForReservation;
        this.closeDiscountDialog();
        this.reserveScenario(scenario);
    }

    reserveWithAppliedDiscounts(): void {
        if (!this.selectedScenarioForDiscount || this.saving) {
            return;
        }

        const scenarioToUse: KonaklamaSenaryoDto = this.scenarioPriceBreakdown
            ? {
                ...this.selectedScenarioForDiscount,
                toplamBazUcret: this.scenarioPriceBreakdown.toplamBazUcret,
                toplamNihaiUcret: this.scenarioPriceBreakdown.toplamNihaiUcret,
                paraBirimi: this.scenarioPriceBreakdown.paraBirimi,
                uygulananIndirimler: [...this.scenarioPriceBreakdown.uygulananIndirimler]
            }
            : this.selectedScenarioForDiscount;

        this.closeDiscountDialog();
        this.reserveScenario(scenarioToUse);
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

        this.degisiklikGecmisiRezervasyonId = kayit.id;
        this.degisiklikGecmisiReferansNo = kayit.referansNo;
        this.degisiklikGecmisiDialogVisible = true;
    }

    closeDegisiklikGecmisiDialog(): void {
        this.degisiklikGecmisiDialogVisible = false;
    }


    openKonaklayanPlaniDialog(kayit: RezervasyonListeDto): void {
        if (!this.canManage || !kayit?.id || kayit.id <= 0) {
            return;
        }

        this.konaklayanPlanRezervasyonId = kayit.id;
        this.konaklayanPlanReferansNo = kayit.referansNo;
        this.konaklayanPlanDialogVisible = true;
    }

    closeKonaklayanPlaniDialog(): void {
        this.konaklayanPlanDialogVisible = false;
        this.konaklayanPlanRezervasyonId = null;
        this.konaklayanPlanReferansNo = '';
    }

    onKonaklayanPlaniSaved(): void {
        const id = this.konaklayanPlanRezervasyonId;
        if (!id) return;
        delete this.rezervasyonDetayById[id];
        this.loadRezervasyonDetay(id);
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
        if (!this.canOpenOdaDegisimDialog(kayit)) {
            return;
        }

        this.odaDegisimRezervasyonId = kayit.id;
        this.odaDegisimReferansNo = kayit.referansNo;
        this.odaDegisimRezervasyonDurumu = kayit.rezervasyonDurumu;
        this.odaDegisimDialogVisible = true;
    }

    closeOdaDegisimDialog(): void {
        this.odaDegisimDialogVisible = false;
        this.odaDegisimRezervasyonId = null;
        this.odaDegisimReferansNo = '';
        this.odaDegisimRezervasyonDurumu = null;
    }

    onOdaDegisimiSaved(): void {
        this.araRezervasyonlar(this.aramaPage);
    }

    openOdemeDialog(kayit: RezervasyonListeDto): void {
        if (!this.canOpenPaymentDialog(kayit)) {
            return;
        }

        this.odemeRezervasyonId = kayit.id;
        this.odemeReferansNo = kayit.referansNo;
        this.odemeRezervasyonDurumu = kayit.rezervasyonDurumu;
        this.odemeTesisId = kayit.tesisId;
        this.odemeMisafirAdiSoyadi = kayit.misafirAdiSoyadi;
        this.odemeMisafirTelefon = kayit.misafirTelefon;
        this.odemeMisafirTcKimlikNo = kayit.tcKimlikNo;
        this.odemeDialogVisible = true;
    }

    closeOdemeDialog(): void {
        this.odemeDialogVisible = false;
        this.odemeRezervasyonId = null;
        this.odemeReferansNo = '';
        this.odemeRezervasyonDurumu = null;
        this.odemeTesisId = null;
        this.odemeMisafirAdiSoyadi = '';
        this.odemeMisafirTelefon = '';
        this.odemeMisafirTcKimlikNo = null;
    }

    onOdemeSaved(result: RezervasyonOdemeOzetDto): void {
        this.applyOdemeOzetiToRezervasyon(result);
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
                    this.syncRezervasyonSummaryAfterCheckOut(kayit.id);
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
        this.tesisService.getTesisler()
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));

                    if (this.selectedTesisId && !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId && this.tesisler.length > 0) {
                        this.selectedTesisId = this.tesisler[0].id ?? null;
                    }

                    if (this.selectedTesisId && this.selectedTesisId > 0) {
                        this.applySelectedTesisDateTimes();
                        this.loadMisafirTipleriByTesis(this.selectedTesisId);
                        this.loadOdaTipleri(this.selectedTesisId);
                        this.loadKonaklamaTipleriByTesis(this.selectedTesisId);
                        this.araRezervasyonlar(1);
                        return;
                    }

                    this.odaTipleri = [];
                    this.misafirTipleri = [];
                    this.konaklamaTipleri = [];
                    this.selectedOdaTipiId = null;
                    this.selectedMisafirTipiId = null;
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

    private loadMisafirTipleriByTesis(tesisId: number): void {
        this.service.getMisafirTipleri(tesisId).subscribe({
            next: (misafirTipleri) => {
                this.misafirTipleri = [...misafirTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));

                if (this.selectedMisafirTipiId && !this.misafirTipleri.some((x) => x.id === this.selectedMisafirTipiId)) {
                    this.selectedMisafirTipiId = null;
                }

                if (!this.selectedMisafirTipiId && this.misafirTipleri.length > 0) {
                    this.selectedMisafirTipiId = this.misafirTipleri[0].id;
                }

                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.misafirTipleri = [];
                this.selectedMisafirTipiId = null;
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

    araRezervasyonlar(page: number = 1): void {
        this.aramaPage = page;
        this.loadingRezervasyonlar = true;
        const request: RezervasyonAramaRequestDto = {
            tesisId: this.selectedTesisId,
            aramaMetni: this.aramaMetni.trim() || null,
            rezervasyonDurumu: this.aramaDurumu || null,
            girisBaslangicTarihi: this.aramaGirisBaslangic ? this.formatLocalDate(this.aramaGirisBaslangic) : null,
            girisBitisTarihi: this.aramaGirisBitis ? this.formatLocalDate(this.aramaGirisBitis) : null,
            cikisBaslangicTarihi: this.aramaCikisBaslangic ? this.formatLocalDate(this.aramaCikisBaslangic) : null,
            cikisBitisTarihi: this.aramaCikisBitis ? this.formatLocalDate(this.aramaCikisBitis) : null,
            sadeceOdemesiKalanlar: this.aramaSadeceOdemesiKalanlar,
            sadeceOdaDegisimiGerekli: this.aramaSadeceOdaDegisimiGerekli,
            page: this.aramaPage,
            pageSize: this.aramaPageSize
        };
        this.service
            .searchRezervasyonlar(request)
            .pipe(
                finalize(() => {
                    this.loadingRezervasyonlar = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (result: RezervasyonAramaSonucDto) => {
                    this.rezervasyonKayitlari = result.kayitlar;
                    this.aramaToplamKayitSayisi = result.toplamKayitSayisi;
                    this.aramaPage = result.page;
                    this.checkActionLoadingByRezervasyonId = {};
                    this.expandedRowKeys = {};
                    this.rezervasyonDetayById = {};
                    this.detayLoadingByRezervasyonId = {};
                    this.closeRezervasyonUcretDetayDialog();
                    this.closeDegisiklikGecmisiDialog();
                    this.closeKonaklayanPlaniDialog();
                    this.closeOdaDegisimDialog();
                    this.closeOdemeDialog();
                    this.handlePendingNavAction();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.rezervasyonKayitlari = [];
                    this.aramaToplamKayitSayisi = 0;
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

    temizleAramaFiltreler(): void {
        this.aramaMetni = '';
        this.aramaDurumu = null;
        this.aramaGirisBaslangic = null;
        this.aramaGirisBitis = null;
        this.aramaCikisBaslangic = null;
        this.aramaCikisBitis = null;
        this.aramaSadeceOdemesiKalanlar = false;
        this.aramaSadeceOdaDegisimiGerekli = false;
        this.araRezervasyonlar(1);
    }

    onAramaSayfaDegisti(event: { first: number; rows: number }): void {
        const newPage = Math.floor(event.first / event.rows) + 1;
        if (newPage !== this.aramaPage) {
            this.araRezervasyonlar(newPage);
        }
    }

    private formatLocalDate(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
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

                    if (!this.canUseSinglePersonPricing) {
                        this.tekKisilikFiyatUygulansinMi = false;
                    }

                    if (clearResults) {
                        this.senaryolar = [];
                    }

                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.odaTipleri = [];
                    this.selectedOdaTipiId = null;
                    this.tekKisilikFiyatUygulansinMi = false;
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

    private syncRezervasyonSummaryAfterCheckOut(rezervasyonId: number): void {
        const kayit = this.rezervasyonKayitlari.find((x) => x.id === rezervasyonId);
        if (!kayit) {
            return;
        }

        const gelen = Math.max(0, kayit.gelenKonaklayanSayisi ?? 0);
        kayit.ayrilanKonaklayanSayisi = Math.max(0, (kayit.ayrilanKonaklayanSayisi ?? 0) + gelen);
        kayit.gelenKonaklayanSayisi = 0;
        kayit.bekleyenKonaklayanSayisi = 0;
    }

    formatDateTime(value: string | Date | null | undefined): string {
        const date = this.parseApiDateTime(value);
        if (!date) {
            return '-';
        }

        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).format(date);
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

    getFiyatlamaOzetiSeverity(fiyatlamaOzeti: string | null | undefined): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch ((fiyatlamaOzeti ?? '').trim()) {
            case 'Tek kisilik fiyat':
                return UiSeverity.Info;
            case 'Kisi basi':
                return UiSeverity.Info;
            case 'Karma':
                return UiSeverity.Warn;
            case 'Ozel kullanim':
                return UiSeverity.Success;
            default:
                return UiSeverity.Secondary;
        }
    }

    get filteredRezervasyonKayitlari(): RezervasyonListeDto[] {
        return this.rezervasyonKayitlari;
    }

    getKonaklayanKatilimChipleri(kayit: RezervasyonListeDto): Array<{ label: string; severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' }> {
        const gelen = Math.max(0, kayit.gelenKonaklayanSayisi ?? 0);
        const bekleyen = Math.max(0, kayit.bekleyenKonaklayanSayisi ?? 0);
        const ayrilan = Math.max(0, kayit.ayrilanKonaklayanSayisi ?? 0);
        const gelmeyen = Math.max(0, (kayit.kisiSayisi ?? 0) - gelen - bekleyen - ayrilan);
        const chips: Array<{ label: string; severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' }> = [];

        if (gelen > 0) {
            chips.push({ label: `Geldi: ${gelen}`, severity: UiSeverity.Success });
        }

        if (bekleyen > 0) {
            chips.push({ label: `Bekliyor: ${bekleyen}`, severity: UiSeverity.Warn });
        }

        if (ayrilan > 0) {
            chips.push({ label: `Ayrildi: ${ayrilan}`, severity: UiSeverity.Info });
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

    private createDefaultCheckInDate(): Date {
        const now = new Date();
        now.setHours(14, 0, 0, 0);
        return now;
    }

    private createDefaultCheckOutDate(): Date {
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        tomorrow.setHours(10, 0, 0, 0);
        return tomorrow;
    }

    private toLocalDateTimeString(value: Date | null | undefined): string {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
            return '';
        }

        const year = String(value.getFullYear()).padStart(4, '0');
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        const hour = String(value.getHours()).padStart(2, '0');
        const minute = String(value.getMinutes()).padStart(2, '0');
        const second = String(value.getSeconds()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}:${second}`;
    }

    private normalizeDateTimeLocalInput(value: string | null | undefined): string {
        if (!value) {
            return '';
        }

        const normalized = value.trim();
        if (normalized.length === 0) {
            return '';
        }

        if (normalized.length === 16) {
            return `${normalized}:00`;
        }

        return normalized;
    }

    private normalizeOptional(value: string): string | null {
        const normalized = value.trim();
        return normalized.length > 0 ? normalized : null;
    }

    private hasSharedRoomUsage(scenario: KonaklamaSenaryoDto): boolean {
        return scenario.segmentler.some((segment) => segment.odaAtamalari.some((assignment) => assignment.paylasimliMi));
    }

    private toDateTimeLocalInputValue(value: string): string {
        const date = this.parseApiDateTime(value);
        if (!date) {
            return value;
        }

        return this.toDateTimeLocalInput(date);
    }

    private parseApiDateTime(value: string | Date | null | undefined): Date | null {
        if (!value) {
            return null;
        }

        if (value instanceof Date) {
            return Number.isNaN(value.getTime()) ? null : new Date(value.getTime());
        }

        const normalized = value.trim();
        if (normalized.length === 0) {
            return null;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
            const [yearText, monthText, dayText] = normalized.split('-');
            const year = Number.parseInt(yearText, 10);
            const month = Number.parseInt(monthText, 10);
            const day = Number.parseInt(dayText, 10);
            const localDate = new Date(year, month - 1, day);
            return Number.isNaN(localDate.getTime()) ? null : localDate;
        }

        const parsed = new Date(normalized);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    private applySelectedTesisDateTimes(): void {
        const selectedTesis = this.tesisler.find((x) => x.id === this.selectedTesisId);
        if (!selectedTesis) {
            return;
        }

        const [girisSaat, girisDakika] = this.parseSaat(selectedTesis.girisSaati, this.defaultGirisSaati);
        const [cikisSaat, cikisDakika] = this.parseSaat(selectedTesis.cikisSaati, this.defaultCikisSaati);

        const baslangic = this.cloneDate(this.baslangicTarihi) ?? this.createDefaultCheckInDate();
        const bitis = this.cloneDate(this.bitisTarihi) ?? this.createDefaultCheckOutDate();

        baslangic.setHours(girisSaat, girisDakika, 0, 0);
        bitis.setHours(cikisSaat, cikisDakika, 0, 0);

        if (bitis.getTime() <= baslangic.getTime()) {
            bitis.setDate(bitis.getDate() + 1);
        }

        this.baslangicTarihi = baslangic;
        this.bitisTarihi = bitis;
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

    private cloneDate(value: Date | string | null | undefined): Date | null {
        const date = this.parseApiDateTime(value);
        return date ? new Date(date.getTime()) : null;
    }

    private toDateTimeLocalInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}`;
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

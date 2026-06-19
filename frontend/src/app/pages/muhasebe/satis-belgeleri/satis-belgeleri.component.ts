import { CommonModule } from '@angular/common';
import { Component, DestroyRef, effect, inject, OnInit, signal } from '@angular/core';
import { toLocalDateString } from '../../../core/utils/date-time.util';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService, MenuItem, MessageService } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SatisBelgesiService } from '../services/satis-belgesi.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { PaketTurleriService } from '../paket-turleri/paket-turleri.service';
import { PaketTuruModel } from '../paket-turleri/paket-turleri.dto';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { CariKartModel, CARI_KART_TIPLERI } from '../cari-kartlar/cari-kartlar.dto';
import { DepolarService } from '../depolar/depolar.service';
import { DepoModel } from '../depolar/depolar.dto';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import { KdvIstisnaTanimDto, createDefaultKdvIstisnaTanimFilter } from '../models/kdv-istisna-tanim.model';
import {
    SatisBelgesiDto,
    SatisBelgesiDurumu,
    SatisBelgesiTipi,
    SatisKaynakModulu,
    SatisBelgesiSatirTipi,
    KdvUygulamaTipi,
    CreateSatisBelgesiRequest,
    UpdateSatisBelgesiRequest,
    SatisBelgesiFilterDto,
    CreateSatisBelgesiSatiriRequest,
    SatisBelgesiRedRequest,
    SATIS_BELGESI_DURUMU_LABELS,
    SATIS_BELGESI_DURUMU_SEVERITIES,
    SATIS_BELGESI_TIPI_LABELS,
    SATIS_KAYNAK_MODULU_LABELS,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    KDV_UYGULAMA_TIPI_LABELS,
    SATIS_BELGESI_DURUM_SECENEKLERI,
    SATIS_BELGE_TIPLERI,
    ALIS_BELGE_TIPLERI,
    createDefaultSatisBelgesiFilter,
    createEmptySatisBelgesiSatiri,
    createEmptyCreateSatisBelgesiRequest,
    getMusteriDisplayName,
} from '../models/satis-belgesi.model';
import { FluidModule } from "primeng/fluid";

type SatirParametreKey = 'indirim' | 'tevkifat' | 'otv' | 'oiv' | 'konaklamaVergisi';

interface SatirParametreDurumu {
    indirim: boolean;
    tevkifat: boolean;
    otv: boolean;
    oiv: boolean;
    konaklamaVergisi: boolean;
}

@Component({
    selector: 'app-satis-belgeleri',
    standalone: true,
    imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    AutoCompleteModule,
    ButtonModule,
    CardModule,
    ConfirmDialogModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    MenuModule,
    SelectModule,
    TableModule,
    TabsModule,
    TagModule,
    TextareaModule,
    ToastModule,
    ToggleSwitchModule,
    ToolbarModule,
    TooltipModule,
    MuhasebeTesisSecimDialogComponent,
    MuhasebeTesisContextBarComponent,
    FluidModule
],
    providers: [ConfirmationService, MessageService],
    templateUrl: './satis-belgeleri.component.html',
    styleUrl: './satis-belgeleri.component.scss'
})
export class SatisBelgeleriComponent implements OnInit {
    private readonly service = inject(SatisBelgesiService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly paketTurleriService = inject(PaketTurleriService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly depoService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);
    private readonly destroyRef = inject(DestroyRef);
    private readonly satirParametreDurumlari = new WeakMap<CreateSatisBelgesiSatiriRequest, SatirParametreDurumu>();
    private readonly satirUiIds = new WeakMap<CreateSatisBelgesiSatiriRequest, number>();
    private readonly collapsedSatirUiIds = signal<number[]>([]);
    private nextSatirUiId = 1;

    // ── State ──
    belgeler = signal<SatisBelgesiDto[]>([]);
    loading = signal(false);
    filter = signal<SatisBelgesiFilterDto>(createDefaultSatisBelgesiFilter());

    // Dialog
    dialogVisible = signal(false);
    dialogLoading = signal(false);
    isEditing = signal(false);
    editingBelge = signal<SatisBelgesiDto | null>(null);
    formData = signal<CreateSatisBelgesiRequest>(createEmptyCreateSatisBelgesiRequest());

    // Cari seçim
    cariKartlar = signal<CariKartModel[]>([]);
    cariKartlarLoading = signal(false);
    selectedCari = signal<CariKartModel | null>(null);
    filteredCariKartlar: CariKartModel[] = [];
    manuelMusteriGirisi = signal(false);

    depoSecenekleri = signal<Array<{ label: string; value: number }>>([]);
    depolarLoading = signal(false);
    paketTurleri = signal<PaketTuruModel[]>([]);
    paketTuruSecenekleri = signal<Array<{ label: string; value: string }>>([]);
    paketTurleriLoading = signal(false);
    tasinirKartlar = signal<TasinirKartModel[]>([]);
    tasinirKartSecenekleri = signal<Array<{ label: string; value: number }>>([]);
    tasinirKartlarLoading = signal(false);
    kdvIstisnaTanimlari = signal<KdvIstisnaTanimDto[]>([]);
    kdvIstisnaTanimlariLoading = signal(false);

    // Reddetme
    redDialogVisible = signal(false);
    redNedeni = signal('');
    redBelgeId = signal<number | null>(null);
    redLoading = signal(false);

    // Detay dialog
    detayDialogVisible = signal(false);
    detayBelge = signal<SatisBelgesiDto | null>(null);

    // Tab navigation
    activeTab = signal('0');
    belgeModu = signal<'satis' | 'alis'>('satis');
    tesisHazir = signal(false);
    private lastLoadedTesisId: number | null = null;

    // ── Label/option maps (plain, not signals) ──
    durumLabels = SATIS_BELGESI_DURUMU_LABELS;
    durumSeverities = SATIS_BELGESI_DURUMU_SEVERITIES;
    durumSecenekleri = SATIS_BELGESI_DURUM_SECENEKLERI;
    belgeTipiLabels = SATIS_BELGESI_TIPI_LABELS;
    kaynakModulLabels = SATIS_KAYNAK_MODULU_LABELS;
    satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;
    kdvEnum = KdvUygulamaTipi;

    belgeTipiSecenekleri: Array<{ value: number; label: string }> = [];
    kaynakModulSecenekleri = Object.entries(this.kaynakModulLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    satirTipiSecenekleri = Object.entries(this.satirTipiLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    kdvUygulamaTipiSecenekleri = Object.entries(this.kdvUygulamaTipiLabels)
        .map(([k, v]) => ({ value: Number(k), label: v }));
    readonly kdvOraniSecenekleri = [
        { label: '%0', value: 0 },
        { label: '%1', value: 1 },
        { label: '%8', value: 8 },
        { label: '%10', value: 10 },
        { label: '%18', value: 18 },
        { label: '%20', value: 20 }
    ];
    readonly satirKartRenkSiniflari = [
        'document-line-card--tone-1',
        'document-line-card--tone-2',
        'document-line-card--tone-3',
        'document-line-card--tone-4',
        'document-line-card--tone-5',
        'document-line-card--tone-6'
    ];
    tevkifatSecenekleri = [
        { label: '2/10', value: '2/10' },
        { label: '3/10', value: '3/10' },
        { label: '4/10', value: '4/10' },
        { label: '5/10', value: '5/10' },
        { label: '7/10', value: '7/10' },
        { label: '9/10', value: '9/10' },
        { label: '10/10', value: '10/10' }
    ];

    getMusteriDisplay = getMusteriDisplayName;

    private readonly tesisDegisimEffect = effect(() => {
        if (!this.tesisHazir()) {
            return;
        }

        const tesis = this.tesisContext.seciliTesis();
        const tesisId = tesis?.id ?? null;
        if (!tesisId) {
            return;
        }

        if (this.lastLoadedTesisId === tesisId) {
            return;
        }

        const tesisDegisti = this.lastLoadedTesisId !== null && this.lastLoadedTesisId !== tesisId;
        this.lastLoadedTesisId = tesisId;

        if (tesisDegisti && this.dialogVisible()) {
            this.dialogVisible.set(false);
            this.messageService.add({
                severity: 'warn',
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için açık form kapatıldı.'
            });
            this.formData.set(createEmptyCreateSatisBelgesiRequest());
            this.selectedCari.set(null);
            this.manuelMusteriGirisi.set(false);
        }

        this.loadLookupLists(tesisId);
        this.loadBelgeler();
    }, { allowSignalWrites: true });

    ngOnInit(): void {
        this.route.data
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(data => {
                const nextMode = data['belgeModu'] === 'alis' ? 'alis' : 'satis';
                const previousMode = this.belgeModu();
                this.applyBelgeModu(nextMode);

                if (previousMode !== nextMode && this.tesisHazir() && this.tesisContext.seciliTesis()?.id) {
                    this.loadLookupLists(this.tesisContext.seciliTesis()!.id);
                    this.loadBelgeler();
                }
            });

        this.tesisContext.initialize().subscribe({
            next: () => {
                this.loadPaketTurleri();
                this.tesisHazir.set(true);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Tesis listesi yüklenemedi: ' + err.message });
            }
        });
    }

    // ── Load ──

    loadBelgeler(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.loading.set(true);
        const currentFilter = this.filter();
        const filterToSend = { ...currentFilter, tesisId, belgeTipleri: this.getBelgeTipleriForMode() };
        this.service.filter(filterToSend).subscribe({
            next: (data) => {
                this.belgeler.set(data);
                this.loading.set(false);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                this.loading.set(false);
            }
        });
    }

    loadLookupLists(tesisId: number): void {
        this.loadCariKartlar(tesisId);
        this.loadDepolar(tesisId);
        this.loadTasinirKartlar(tesisId);
        this.loadKdvIstisnaTanimlari();
    }

    loadPaketTurleri(): void {
        this.paketTurleriLoading.set(true);
        this.paketTurleriService.getAll().pipe(
            finalize(() => this.paketTurleriLoading.set(false))
        ).subscribe({
            next: (data) => {
                const aktifPaketTurleri = data
                    .filter(t => t.aktifMi)
                    .sort((a, b) => (a.kisaAd || a.ad || '').localeCompare(b.kisaAd || b.ad || ''));

                const secenekler = aktifPaketTurleri.map(t => ({
                    label: t.kisaAd ? `${t.kisaAd} - ${t.ad}` : t.ad,
                    value: t.ad
                }));
                this.paketTurleri.set(aktifPaketTurleri);

                if (secenekler.length === 0) {
                    this.messageService.add({
                        severity: 'warn',
                        summary: 'Paket Türleri',
                        detail: 'Aktif paket türü bulunamadı. Birim alanı varsayılan Adet ile devam edecek.'
                    });
                }

                this.paketTuruSecenekleri.set(secenekler.length > 0 ? secenekler : [{ label: 'Adet', value: 'Adet' }]);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Paket türleri yüklenemedi: ' + err.message });
                this.paketTuruSecenekleri.set([{ label: 'Adet', value: 'Adet' }]);
            }
        });
    }

    applyBelgeModu(mod: 'satis' | 'alis'): void {
        this.belgeModu.set(mod);
        this.belgeTipiSecenekleri = this.getBelgeTipiSecenekleri();
    }

    isAlisMode(): boolean {
        return this.belgeModu() === 'alis';
    }

    isSatisMode(): boolean {
        return this.belgeModu() === 'satis';
    }

    getBelgeTipiSecenekleri(): Array<{ value: number; label: string }> {
        const tipler = this.isAlisMode() ? ALIS_BELGE_TIPLERI : SATIS_BELGE_TIPLERI;
        return tipler.map(tip => ({ value: tip, label: this.getBelgeTipiLabel(tip) }));
    }

    getDefaultBelgeTipi(): SatisBelgesiTipi {
        return this.isAlisMode() ? SatisBelgesiTipi.AlisFaturasi : SatisBelgesiTipi.FaturaTaslagi;
    }

    getBelgeTipleriForMode(): SatisBelgesiTipi[] {
        return this.isAlisMode() ? ALIS_BELGE_TIPLERI : SATIS_BELGE_TIPLERI;
    }

    getScreenTitle(): string {
        return this.isAlisMode() ? 'Alış Belgeleri / Satın Alma Faturaları' : 'Satış Belgeleri / Fatura Taslakları';
    }

    getNewBelgeButtonLabel(): string {
        return this.isAlisMode() ? 'Yeni Alış Belgesi' : 'Yeni Belge';
    }

    getDialogHeader(): string {
        return this.isAlisMode() ? (this.isEditing() ? 'Alış Belgesi Düzenle' : 'Yeni Alış Belgesi') : (this.isEditing() ? 'Satış Belgesi Düzenle' : 'Yeni Satış Belgesi');
    }

    getCariPanelTitle(): string {
        return this.isAlisMode() ? '👤 Cari / Tedarikçi Bilgileri' : '👤 Cari / Müşteri Bilgileri';
    }

    getCariSectionLabel(): string {
        return this.isAlisMode() ? 'Tedarikçi Cari Kart' : 'Cari Kart';
    }

    getCariPlaceholder(): string {
        return this.isAlisMode()
            ? 'Tedarikçi cari seçiniz (unvan, vergi no veya TCKN ile arayın)...'
            : 'Cari kart seçiniz (unvan, vergi no veya TCKN ile arayın)...';
    }

    getCariHelperText(): string {
        return this.isAlisMode()
            ? 'Tedarikçi seçtiğinizde bilgiler otomatik doldurulur.'
            : 'Cari kart seçtiğinizde müşteri bilgileri otomatik doldurulur.';
    }

    getCariEmptyStateTitle(): string {
        return this.isAlisMode()
            ? 'Tedarikçi bilgisi bekleniyor'
            : 'Cari bilgisi bekleniyor';
    }

    getCariEmptyStateMessage(): string {
        return this.isAlisMode()
            ? 'Tedarikçi cari kart seçiniz veya manuel cari bilgisi girişi seçeneğini açınız.'
            : 'Cari kart seçiniz veya manuel müşteri bilgisi girişi seçeneğini açınız.';
    }

    getOtomatikCariTagValue(): string {
        return this.isAlisMode() ? 'Tedarikçiden otomatik' : 'Cari\'den otomatik';
    }

    getCariOzetBasligi(): string {
        return this.isAlisMode() ? 'Tedarikçi Snapshot' : 'Müşteri Snapshot';
    }

    getCariOzetPanelBasligi(): string {
        return this.isAlisMode() ? '👤 Tedarikçi / Cari Bilgisi' : '👤 Müşteri / Cari Bilgisi';
    }

    showCariSnapshot(): boolean {
        return !this.manuelMusteriGirisi() && !!this.selectedCari();
    }

    showCariManualForm(): boolean {
        return this.manuelMusteriGirisi();
    }

    showCariEmptyState(): boolean {
        return !this.manuelMusteriGirisi() && !this.selectedCari();
    }

    getCariColumnBasligi(): string {
        return this.isAlisMode() ? 'Tedarikçi' : 'Müşteri';
    }

    getCariTipleriForMode(): string[] {
        return this.isAlisMode()
            ? [CARI_KART_TIPLERI.Tedarikci]
            : [CARI_KART_TIPLERI.Musteri, CARI_KART_TIPLERI.KurumsalMusteri];
    }

    loadCariKartlar(tesisId: number): void {
        this.cariKartlarLoading.set(true);
        this.cariKartService.getAll(tesisId).pipe(
            finalize(() => this.cariKartlarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const allowedTypes = this.getCariTipleriForMode();
                const cariList = data
                    .filter(c => c.aktifMi && allowedTypes.includes(c.cariTipi))
                    .sort((a, b) => (a.cariKodu ?? '').localeCompare(b.cariKodu ?? ''));
                this.cariKartlar.set(cariList);
                this.filteredCariKartlar = [...cariList];
                this.syncSelectedCariWithLookup(cariList);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Cari kartlar yüklenemedi: ' + err.message });
            }
        });
    }

    loadDepolar(tesisId: number): void {
        this.depolarLoading.set(true);
        this.depoService.getAll(tesisId).pipe(
            finalize(() => this.depolarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const depolar = data
                    .filter(d => d.aktifMi)
                    .sort((a, b) => (a.kod ?? '').localeCompare(b.kod ?? ''));
                this.depoSecenekleri.set(
                    depolar
                        .filter(d => !!d.id)
                        .map(d => ({ label: `${d.kod} - ${d.ad}`, value: d.id! }))
                );
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Depolar yüklenemedi: ' + err.message });
            }
        });
    }

    loadTasinirKartlar(tesisId: number): void {
        this.tasinirKartlarLoading.set(true);
        this.tasinirKartService.getAll(tesisId).pipe(
            finalize(() => this.tasinirKartlarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const kartlar = data
                    .filter(k => k.aktifMi)
                    .sort((a, b) => (a.stokKodu ?? '').localeCompare(b.stokKodu ?? ''));
                this.tasinirKartlar.set(kartlar);
                this.tasinirKartSecenekleri.set(
                    kartlar
                        .filter(k => !!k.id)
                        .map(k => ({ label: `${k.stokKodu} - ${k.ad} (${k.birim})`, value: k.id! }))
                );
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Taşınır kartlar yüklenemedi: ' + err.message });
            }
        });
    }

    loadKdvIstisnaTanimlari(): void {
        this.kdvIstisnaTanimlariLoading.set(true);
        const filter = createDefaultKdvIstisnaTanimFilter();
        filter.aktifMi = true;
        if (this.isAlisMode()) {
            filter.alisIslemlerindeKullanilirMi = true;
        } else {
            filter.satisIslemlerindeKullanilirMi = true;
        }
        this.kdvIstisnaTanimService.filter(filter).pipe(
            finalize(() => this.kdvIstisnaTanimlariLoading.set(false))
        ).subscribe({
            next: (data) => {
                const list = data
                    .filter(t => t.aktifMi && (this.isAlisMode() ? t.alisIslemlerindeKullanilirMi : t.satisIslemlerindeKullanilirMi))
                    .sort((a, b) => `${a.kod} ${a.ad}`.localeCompare(`${b.kod} ${b.ad}`));
                this.kdvIstisnaTanimlari.set(list);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'KDV istisna tanımları yüklenemedi: ' + err.message });
            }
        });
    }

    onFilterFieldChange<K extends keyof SatisBelgesiFilterDto>(key: K, value: SatisBelgesiFilterDto[K]): void {
        this.filter.update(f => ({ ...f, [key]: value }));
        this.loadBelgeler();
    }

    updateFormField<K extends keyof CreateSatisBelgesiRequest>(key: K, value: CreateSatisBelgesiRequest[K]): void {
        this.formData.update(f => ({ ...f, [key]: value }));
    }

    onFilterChange(): void {
        this.loadBelgeler();
    }

    clearFilter(): void {
        this.filter.set(createDefaultSatisBelgesiFilter());
        this.loadBelgeler();
    }

    private syncSelectedCariWithLookup(cariList: CariKartModel[]): void {
        const current = this.selectedCari();
        if (!current) {
            return;
        }

        const matched = cariList.find(c =>
            c.id === current.id ||
            (!!c.vergiNoTckn && c.vergiNoTckn === current.vergiNoTckn && c.unvanAdSoyad === current.unvanAdSoyad));

        if (matched) {
            this.selectedCari.set(matched);
            this.formData.update(f => ({ ...f, cariKartId: matched.id ?? null }));
        } else if (!this.manuelMusteriGirisi()) {
            this.selectedCari.set(null);
            this.formData.update(f => ({ ...f, cariKartId: null }));
            this.messageService.add({
                severity: 'warn',
                summary: 'Cari Kart Uyarısı',
                detail: 'Seçili cari kart mevcut çalışma tesisinde bulunamadığı için temizlendi.'
            });
        }
    }

    private syncSelectedCariFromBelge(belge: SatisBelgesiDto): void {
        const cariList = this.cariKartlar();
        const matched = belge.cariKartId
            ? cariList.find(c => c.id === belge.cariKartId)
            : cariList.find(c =>
                (belge.musteriVergiNo && c.vergiNoTckn === belge.musteriVergiNo) ||
                (belge.musteriTcKimlikNo && c.vergiNoTckn === belge.musteriTcKimlikNo) ||
                (belge.kurumsalMi && c.unvanAdSoyad === belge.musteriUnvan) ||
                (!belge.kurumsalMi && c.unvanAdSoyad === belge.musteriAdSoyad)
            );

        this.selectedCari.set(matched ?? null);
        this.formData.update(f => ({ ...f, cariKartId: matched?.id ?? belge.cariKartId ?? null }));
    }

    // ── Cari Kart AutoComplete ──

    filterCari(event: { query: string }): void {
        const query = (event.query ?? '').toLowerCase().trim();
        const allCari = this.cariKartlar();
        if (!query) {
            this.filteredCariKartlar = [...allCari];
            return;
        }
        this.filteredCariKartlar = allCari.filter(c =>
            (c.unvanAdSoyad ?? '').toLowerCase().includes(query) ||
            (c.vergiNoTckn ?? '').toLowerCase().includes(query) ||
            (c.cariKodu ?? '').toLowerCase().includes(query)
        );
    }

    onCariKartSecildi(cari: CariKartModel | null): void {
        if (!cari) {
            this.selectedCari.set(null);
            this.formData.update(f => ({ ...f, cariKartId: null }));
            if (!this.manuelMusteriGirisi()) {
                this.formData.update(f => ({
                    ...f,
                    kurumsalMi: false,
                    musteriUnvan: null,
                    musteriAdSoyad: null,
                    musteriVergiNo: null,
                    musteriTcKimlikNo: null,
                    musteriVergiDairesi: null,
                    musteriAdres: null,
                    musteriEposta: null,
                    musteriTelefon: null
                }));
            }
            return;
        }

        if (!this.getCariTipleriForMode().includes(cari.cariTipi)) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Cari Tipi Uyuşmuyor',
                detail: this.isAlisMode()
                    ? 'Alış belgelerinde yalnızca tedarikçi cari kartları seçilebilir.'
                    : 'Satış belgelerinde yalnızca müşteri cari kartları seçilebilir.'
            });
            this.selectedCari.set(null);
            this.formData.update(f => ({ ...f, cariKartId: null }));
            if (!this.manuelMusteriGirisi()) {
                this.formData.update(f => ({
                    ...f,
                    kurumsalMi: false,
                    musteriUnvan: null,
                    musteriAdSoyad: null,
                    musteriVergiNo: null,
                    musteriTcKimlikNo: null,
                    musteriVergiDairesi: null,
                    musteriAdres: null,
                    musteriEposta: null,
                    musteriTelefon: null
                }));
            }
            return;
        }

        this.manuelMusteriGirisi.set(false);
        this.selectedCari.set(cari);
        this.formData.update(f => ({ ...f, cariKartId: cari.id ?? null }));
        // Satışta müşteri tipi, alışta tedarikçi snapshot'ı kurumsal varsayılır
        const kurumsalMi = this.isAlisMode() || cari.cariTipi === 'KurumsalMusteri';

        // Manuel müşteri girişi kapalıyken cari bilgilerini form alanlarına yaz
        if (!this.manuelMusteriGirisi()) {
            this.formData.update(f => ({
                ...f,
                kurumsalMi,
                musteriUnvan: kurumsalMi ? cari.unvanAdSoyad : null,
                musteriAdSoyad: !kurumsalMi ? cari.unvanAdSoyad : null,
                musteriVergiNo: kurumsalMi ? (cari.vergiNoTckn ?? null) : null,
                musteriTcKimlikNo: !kurumsalMi ? (cari.vergiNoTckn ?? null) : null,
                musteriVergiDairesi: cari.vergiDairesi ?? null,
                musteriAdres: cari.adres ?? null,
                musteriEposta: cari.eposta ?? null,
                musteriTelefon: cari.telefon ?? null
            }));
        }
    }

    onManuelMusteriGirisiChange(value: boolean): void {
        this.manuelMusteriGirisi.set(value);
        if (value) {
            this.selectedCari.set(null);
            this.formData.update(f => ({ ...f, cariKartId: null }));
        } else if (this.selectedCari()) {
            // Manuel mod kapandı — seçili cari varsa bilgileri tekrar doldur
            this.onCariKartSecildi(this.selectedCari());
        }
    }

    // Cari display için format
    formatCariDisplay(cari: CariKartModel): string {
        const kod = cari.cariKodu || '-';
        const unvan = cari.unvanAdSoyad || '-';
        const vergi = cari.vergiNoTckn ? ` (${cari.vergiNoTckn})` : '';
        return `${kod} - ${unvan}${vergi}`;
    }

    // ── Create / Edit Dialog ──

    onTabChange(value: string | number | undefined): void {
        if (value !== undefined) {
            this.activeTab.set(String(value));
        }
    }

    openCreateDialog(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.isEditing.set(false);
        this.editingBelge.set(null);
        const empty = createEmptyCreateSatisBelgesiRequest();
        empty.belgeTipi = this.getDefaultBelgeTipi();
        empty.tesisId = tesisId;
        empty.satirlar.forEach(satir => {
            satir.birim = this.resolveDefaultBirim();
        });
        this.formData.set(empty);
        this.initializeSatirParametreDurumlari(empty.satirlar);
        this.selectedCari.set(null);
        this.manuelMusteriGirisi.set(false);
        this.filteredCariKartlar = [...this.cariKartlar()];
        this.activeTab.set('0');
        this.dialogVisible.set(true);
    }

    openEditDialog(belge: SatisBelgesiDto): void {
        if (belge.durum !== SatisBelgesiDurumu.Taslak && belge.durum !== SatisBelgesiDurumu.Reddedildi) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Sadece Taslak veya Reddedildi durumundaki belgeler düzenlenebilir.' });
            return;
        }
        this.isEditing.set(true);
        this.editingBelge.set(belge);
        this.formData.set({
            belgeTipi: belge.belgeTipi,
            kaynakModul: belge.kaynakModul,
            kaynakTipi: belge.kaynakTipi,
            kaynakId: belge.kaynakId,
            tesisId: belge.tesisId,
            cariKartId: belge.cariKartId ?? null,
            belgeTarihi: belge.belgeTarihi?.split('T')[0] ?? (toLocalDateString(new Date()) ?? ''),
            vadeTarihi: belge.vadeTarihi?.split('T')[0] ?? null,
            musteriUnvan: belge.musteriUnvan,
            musteriAdSoyad: belge.musteriAdSoyad,
            musteriVergiNo: belge.musteriVergiNo,
            musteriTcKimlikNo: belge.musteriTcKimlikNo,
            musteriVergiDairesi: belge.musteriVergiDairesi,
            musteriAdres: belge.musteriAdres,
            musteriEposta: belge.musteriEposta,
            musteriTelefon: belge.musteriTelefon,
            kurumsalMi: belge.kurumsalMi,
            aciklama: belge.aciklama,
            belgeNo: belge.belgeNo,
                satirlar: belge.satirlar.map(s => ({
                siraNo: s.siraNo,
                satirTipi: s.satirTipi,
                aciklama: s.aciklama,
                tasinirKartId: s.tasinirKartId ?? null,
                depoId: s.depoId ?? null,
                birim: s.birim || this.resolveDefaultBirim(),
                miktar: s.miktar,
                birimFiyat: s.birimFiyat,
                indirimOrani: s.indirimOrani,
                indirimTutari: s.indirimTutari,
                kdvUygulamaTipi: s.kdvUygulamaTipi,
                kdvIstisnaTanimId: s.kdvIstisnaTanimId,
                kdvOrani: s.kdvOrani,
                tevkifatPay: s.tevkifatPay ?? null,
                tevkifatPayda: s.tevkifatPayda ?? null,
                otvOrani: s.otvOrani,
                otvTutari: s.otvTutari,
                oivOrani: s.oivOrani,
                oivTutari: s.oivTutari,
                konaklamaVergisiOrani: s.konaklamaVergisiOrani,
                konaklamaVergisiTutari: s.konaklamaVergisiTutari,
                kaynakSatirId: s.kaynakSatirId
            }))
        });
        this.syncSelectedCariFromBelge(belge);
        this.manuelMusteriGirisi.set(false);
        this.filteredCariKartlar = [...this.cariKartlar()];
        this.initializeSatirParametreDurumlari(this.formData().satirlar);
        this.activeTab.set('0');
        this.dialogVisible.set(true);
    }

    saveBelge(): void {
        // Validation: Cari seçilmemiş ve manuel mod kapalıysa uyar
        if (!this.manuelMusteriGirisi() && !this.selectedCari()) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: this.isAlisMode()
                    ? 'Lütfen tedarikçi cari kart seçiniz veya "Manuel cari bilgisi gireceğim" seçeneğini açınız.'
                    : 'Lütfen cari kart seçiniz veya "Manuel müşteri bilgisi gireceğim" seçeneğini açınız.'
            });
            return;
        }

        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.dialogLoading.set(true);
        if (this.isEditing()) {
            const id = this.editingBelge()!.id;
            const payload = {
                ...this.formData(),
                tesisId,
                satirlar: this.formData().satirlar.map(s => this.mapSatirToRequest(s))
            };
            const updateReq: UpdateSatisBelgesiRequest = {
                belgeNo: payload.belgeNo,
                belgeTipi: payload.belgeTipi,
                tesisId: payload.tesisId,
                cariKartId: payload.cariKartId,
                belgeTarihi: payload.belgeTarihi,
                vadeTarihi: payload.vadeTarihi,
                musteriUnvan: payload.musteriUnvan,
                musteriAdSoyad: payload.musteriAdSoyad,
                musteriVergiNo: payload.musteriVergiNo,
                musteriTcKimlikNo: payload.musteriTcKimlikNo,
                musteriVergiDairesi: payload.musteriVergiDairesi,
                musteriAdres: payload.musteriAdres,
                musteriEposta: payload.musteriEposta,
                musteriTelefon: payload.musteriTelefon,
                kurumsalMi: payload.kurumsalMi,
                aciklama: payload.aciklama,
                satirlar: payload.satirlar
            };
            this.service.update(id, updateReq).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge güncellendi.' });
                    this.dialogVisible.set(false);
                    this.dialogLoading.set(false);
                    this.loadBelgeler();
                },
                error: (err) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                    this.dialogLoading.set(false);
                }
            });
        } else {
            const payload = {
                ...this.formData(),
                tesisId,
                satirlar: this.formData().satirlar.map(s => this.mapSatirToRequest(s))
            };
            this.service.create(payload).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge oluşturuldu.' });
                    this.dialogVisible.set(false);
                    this.dialogLoading.set(false);
                    this.loadBelgeler();
                },
                error: (err) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                    this.dialogLoading.set(false);
                }
            });
        }
    }

    // ── Sil ──

    confirmDelete(belge: SatisBelgesiDto): void {
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" belgesini silmek istediğinize emin misiniz?`,
            header: 'Silme Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.delete(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge silindi.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Durum Aksiyonları ──

    muhasebeOnayinaGonder(belge: SatisBelgesiDto): void {
        this.service.muhasebeOnayinaGonder(belge.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge muhasebe onayına gönderildi.' });
                this.loadBelgeler();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    muhasebeOnayla(belge: SatisBelgesiDto): void {
        this.service.muhasebeOnayla(belge.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge muhasebe tarafından onaylandı.' });
                this.loadBelgeler();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    openRedDialog(belge: SatisBelgesiDto): void {
        this.redBelgeId.set(belge.id);
        this.redNedeni.set('');
        this.redDialogVisible.set(true);
    }

    reddet(): void {
        if (!this.redNedeni().trim()) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Ret nedeni zorunludur.' });
            return;
        }
        this.redLoading.set(true);
        const req: SatisBelgesiRedRequest = { redNedeni: this.redNedeni() };
        this.service.reddet(this.redBelgeId()!, req).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge reddedildi.' });
                this.redDialogVisible.set(false);
                this.redLoading.set(false);
                this.loadBelgeler();
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                this.redLoading.set(false);
            }
        });
    }

    iptalEt(belge: SatisBelgesiDto): void {
        const message = belge.muhasebeFisId
            ? `"${belge.belgeNo}" belgesi iptal edilecek. Bağlı muhasebe fişi ters kayıtla iptal edilecek, stok ve cari hareketler iptal durumuna alınacaktır. Devam etmek istiyor musunuz?`
            : `"${belge.belgeNo}" belgesini iptal etmek istediğinize emin misiniz?`;

        this.confirmationService.confirm({
            message,
            header: 'İptal Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.iptalEt(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge iptal edildi.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Detay ──

    openDetayDialog(belge: SatisBelgesiDto): void {
        this.detayBelge.set(belge);
        this.detayDialogVisible.set(true);
    }

    // ── Satır Yönetimi ──

    addSatir(): void {
        const satirlar = [...this.formData().satirlar];
        const yeniSatir = createEmptySatisBelgesiSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        yeniSatir.birim = this.resolveDefaultBirim();
        this.initializeSatirParametreDurumlari([yeniSatir]);
        satirlar.push(yeniSatir);
        this.formData.update(f => ({ ...f, satirlar }));
    }

    removeSatir(index: number): void {
        if (this.formData().satirlar.length <= 1) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'En az bir satır olmalıdır.' });
            return;
        }
        const removedSatir = this.formData().satirlar[index] ?? null;
        const removedSatirUiId = removedSatir ? this.getSatirUiId(removedSatir) : null;
        const satirlar = this.formData().satirlar.filter((_, i) => i !== index);
        satirlar.forEach((s, i) => s.siraNo = i + 1);
        this.formData.update(f => ({ ...f, satirlar }));
        if (removedSatirUiId !== null) {
            this.collapsedSatirUiIds.update(ids => ids.filter(id => id !== removedSatirUiId));
        }
    }

    isSatirExpanded(satir: CreateSatisBelgesiSatiriRequest): boolean {
        return !this.collapsedSatirUiIds().includes(this.getSatirUiId(satir));
    }

    toggleSatirExpanded(satir: CreateSatisBelgesiSatiriRequest): void {
        const satirUiId = this.getSatirUiId(satir);
        this.collapsedSatirUiIds.update(ids => {
            const next = new Set(ids);
            if (next.has(satirUiId)) {
                next.delete(satirUiId);
            } else {
                next.add(satirUiId);
            }
            return [...next];
        });
    }

    getSatirKartRenkClass(index: number): string {
        return this.satirKartRenkSiniflari[index % this.satirKartRenkSiniflari.length];
    }

    // ── Helpers ──

    getSeciliTesisIdOrWarn(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: 'warn',
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe işlemi için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    private initializeSatirParametreDurumlari(satirlar: CreateSatisBelgesiSatiriRequest[]): void {
        satirlar.forEach(satir => {
            this.getSatirParametreDurumu(satir);
            this.getSatirUiId(satir);
        });
    }

    private getSatirUiId(satir: CreateSatisBelgesiSatiriRequest): number {
        const mevcut = this.satirUiIds.get(satir);
        if (mevcut !== undefined) {
            return mevcut;
        }

        const yeniId = this.nextSatirUiId++;
        this.satirUiIds.set(satir, yeniId);
        return yeniId;
    }

    private getSatirParametreDurumu(satir: CreateSatisBelgesiSatiriRequest): SatirParametreDurumu {
        const mevcut = this.satirParametreDurumlari.get(satir);
        if (mevcut) {
            return mevcut;
        }

        const yeniDurum: SatirParametreDurumu = {
            indirim: this.getSatirIndirimOrani(satir) > 0 || this.getSatirIndirimTutari(satir) > 0,
            tevkifat: satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli ||
                this.getSatirTevkifatTutari(satir) > 0 ||
                this.hasTevkifatliParametre(satir),
            otv: this.getSatirOtvOrani(satir) > 0 || this.getSatirOtvTutari(satir) > 0,
            oiv: this.getSatirOivOrani(satir) > 0 || this.getSatirOivTutari(satir) > 0,
            konaklamaVergisi: this.getSatirKonaklamaVergisiOrani(satir) > 0 || this.getSatirKonaklamaVergisiTutari(satir) > 0
        };

        this.satirParametreDurumlari.set(satir, yeniDurum);
        return yeniDurum;
    }

    private hasTevkifatliParametre(satir: CreateSatisBelgesiSatiriRequest): boolean {
        return (satir.tevkifatPay ?? 0) > 0 && (satir.tevkifatPayda ?? 0) > 0;
    }

    private setSatirParametreGorunurlugu(
        satir: CreateSatisBelgesiSatiriRequest,
        parametre: SatirParametreKey,
        visible: boolean): void {
        const durum = this.getSatirParametreDurumu(satir);
        durum[parametre] = visible;

        if (!visible) {
            this.clearSatirParametre(satir, parametre);
        } else if (parametre === 'tevkifat') {
            if (satir.kdvUygulamaTipi !== KdvUygulamaTipi.Tevkifatli) {
                this.onSatirKdvTipiChange(satir, KdvUygulamaTipi.Tevkifatli);
            }
        }
    }

    private clearSatirParametre(satir: CreateSatisBelgesiSatiriRequest, parametre: SatirParametreKey): void {
        switch (parametre) {
            case 'indirim':
                satir.indirimOrani = 0;
                satir.indirimTutari = 0;
                break;
            case 'tevkifat':
                satir.tevkifatPay = null;
                satir.tevkifatPayda = null;
                if (satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
                    this.onSatirKdvTipiChange(satir, KdvUygulamaTipi.Kdvli);
                }
                break;
            case 'otv':
                satir.otvOrani = 0;
                satir.otvTutari = 0;
                break;
            case 'oiv':
                satir.oivOrani = 0;
                satir.oivTutari = 0;
                break;
            case 'konaklamaVergisi':
                satir.konaklamaVergisiOrani = 0;
                satir.konaklamaVergisiTutari = 0;
                break;
        }
    }

    private mapSatirToRequest(satir: CreateSatisBelgesiSatiriRequest): CreateSatisBelgesiSatiriRequest {
        const brut = this.getSatirBrutTutar(satir);
        const indirimOrani = this.getSatirIndirimOrani(satir);
        const indirimTutari = this.calculateRateBasedAmount(brut, indirimOrani, satir.indirimTutari);
        const matrah = Math.max(0, brut - indirimTutari);
        const otvOrani = this.getSatirOtvOrani(satir);
        const otvTutari = this.calculateRateBasedAmount(matrah, otvOrani, satir.otvTutari);
        const oivOrani = this.getSatirOivOrani(satir);
        const oivTutari = this.calculateRateBasedAmount(matrah, oivOrani, satir.oivTutari);
        const konaklamaVergisiOrani = this.getSatirKonaklamaVergisiOrani(satir);
        const konaklamaVergisiTutari = this.calculateRateBasedAmount(matrah, konaklamaVergisiOrani, satir.konaklamaVergisiTutari);

        return {
            ...satir,
            indirimOrani,
            indirimTutari,
            otvOrani,
            otvTutari,
            oivOrani,
            oivTutari,
            konaklamaVergisiOrani,
            konaklamaVergisiTutari
        };
    }

    isSatirParametreGorunur(satir: CreateSatisBelgesiSatiriRequest, parametre: SatirParametreKey): boolean {
        return this.getSatirParametreDurumu(satir)[parametre];
    }

    hasSatirParametreleri(satir: CreateSatisBelgesiSatiriRequest): boolean {
        const durum = this.getSatirParametreDurumu(satir);
        return durum.indirim || durum.tevkifat || durum.otv || durum.oiv || durum.konaklamaVergisi;
    }

    toggleSatirParametre(satir: CreateSatisBelgesiSatiriRequest, parametre: SatirParametreKey): void {
        const durum = this.getSatirParametreDurumu(satir);
        this.setSatirParametreGorunurlugu(satir, parametre, !durum[parametre]);
    }

    getSatirParametreMenuItems(satir: CreateSatisBelgesiSatiriRequest): MenuItem[] {
        const buildItem = (parametre: SatirParametreKey, label: string): MenuItem => ({
            label: this.isSatirParametreGorunur(satir, parametre) ? `${label} Kaldır` : `${label} Ekle`,
            icon: this.isSatirParametreGorunur(satir, parametre) ? 'pi pi-times' : 'pi pi-plus',
            command: () => this.toggleSatirParametre(satir, parametre)
        });

        return [
            buildItem('indirim', 'İndirim'),
            buildItem('tevkifat', 'Tevkifat Oranı'),
            buildItem('otv', 'ÖTV'),
            buildItem('oiv', 'ÖİV'),
            buildItem('konaklamaVergisi', 'Konaklama Vergisi')
        ];
    }

    private normalizeLookupValue(value?: string | null): string {
        return (value ?? '').trim().toLocaleLowerCase('tr-TR');
    }

    private normalizeRate(rate?: number | null): number {
        return Math.max(0, rate ?? 0);
    }

    private calculateRateBasedAmount(baseAmount: number, rate?: number | null, fallbackAmount?: number | null): number {
        const normalizedRate = this.normalizeRate(rate);
        if (normalizedRate > 0) {
            return Math.max(0, Math.round(baseAmount * normalizedRate / 100 * 100) / 100);
        }

        return Math.max(0, fallbackAmount ?? 0);
    }

    getSatirBrutTutar(satir: CreateSatisBelgesiSatiriRequest): number {
        return Math.max(0, (satir.miktar ?? 0) * (satir.birimFiyat ?? 0));
    }

    resolveDefaultBirim(): string {
        const secenekler = this.paketTuruSecenekleri();
        if (secenekler.length === 0) {
            return 'Adet';
        }

        const adet = secenekler.find(opt =>
            this.normalizeLookupValue(opt.value) === 'adet' ||
            this.normalizeLookupValue(opt.label) === 'adet');

        return adet?.value ?? secenekler[0]?.value ?? 'Adet';
    }

    getBirimSecenekleri(satir?: CreateSatisBelgesiSatiriRequest): Array<{ label: string; value: string }> {
        const secenekler = [...this.paketTuruSecenekleri()];
        const currentValue = satir?.birim?.trim();

        if (currentValue && !secenekler.some(opt => opt.value === currentValue)) {
            secenekler.unshift({ label: currentValue, value: currentValue });
        }

        if (secenekler.length === 0) {
            secenekler.push({ label: 'Adet', value: 'Adet' });
        }

        return secenekler;
    }

    getSatirIndirimOrani(satir: CreateSatisBelgesiSatiriRequest): number {
        const direct = this.normalizeRate(satir.indirimOrani);
        if (direct > 0) {
            return direct;
        }

        const brut = this.getSatirBrutTutar(satir);
        if (brut <= 0 || this.getSatirIndirimTutari(satir) <= 0) {
            return 0;
        }

        return Math.max(0, Math.round((this.getSatirIndirimTutari(satir) * 100 / brut) * 10000) / 10000);
    }

    getSatirIndirimTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        const brut = this.getSatirBrutTutar(satir);
        return this.calculateRateBasedAmount(brut, satir.indirimOrani, satir.indirimTutari);
    }

    getSatirMatrah(satir: CreateSatisBelgesiSatiriRequest): number {
        const brut = this.getSatirBrutTutar(satir);
        const indirim = this.getSatirIndirimTutari(satir);
        return Math.max(0, brut - indirim);
    }

    getSatirBrutKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.TamIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KismiIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KdvKapsamDisi) {
            return 0;
        }

        return this.getSatirMatrah(satir) * (satir.kdvOrani ?? 0) / 100;
    }

    getSatirTevkifatTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        if (satir.kdvUygulamaTipi !== KdvUygulamaTipi.Tevkifatli) {
            return 0;
        }

        const brutKdv = this.getSatirBrutKdvTutari(satir);
        const pay = satir.tevkifatPay ?? 0;
        const payda = satir.tevkifatPayda ?? 0;
        if (pay <= 0 || payda <= 0) {
            return 0;
        }

        return brutKdv * pay / payda;
    }

    previewNetKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirBrutKdvTutari(satir) - this.getSatirTevkifatTutari(satir);
    }

    previewKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirBrutKdvTutari(satir);
    }

    getSatirOtvOrani(satir: CreateSatisBelgesiSatiriRequest): number {
        const direct = this.normalizeRate(satir.otvOrani);
        if (direct > 0) {
            return direct;
        }

        const tutar = this.getSatirOtvTutari(satir);
        const matrah = this.getSatirMatrah(satir);
        if (matrah <= 0 || tutar <= 0) {
            return 0;
        }

        return Math.max(0, Math.round((tutar * 100 / matrah) * 10000) / 10000);
    }

    getSatirOtvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.calculateRateBasedAmount(this.getSatirMatrah(satir), satir.otvOrani, satir.otvTutari);
    }

    getSatirOivOrani(satir: CreateSatisBelgesiSatiriRequest): number {
        const direct = this.normalizeRate(satir.oivOrani);
        if (direct > 0) {
            return direct;
        }

        const tutar = this.getSatirOivTutari(satir);
        const matrah = this.getSatirMatrah(satir);
        if (matrah <= 0 || tutar <= 0) {
            return 0;
        }

        return Math.max(0, Math.round((tutar * 100 / matrah) * 10000) / 10000);
    }

    getSatirOivTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.calculateRateBasedAmount(this.getSatirMatrah(satir), satir.oivOrani, satir.oivTutari);
    }

    getSatirKonaklamaVergisiOrani(satir: CreateSatisBelgesiSatiriRequest): number {
        const direct = this.normalizeRate(satir.konaklamaVergisiOrani);
        if (direct > 0) {
            return direct;
        }

        const tutar = this.getSatirKonaklamaVergisiTutari(satir);
        const matrah = this.getSatirMatrah(satir);
        if (matrah <= 0 || tutar <= 0) {
            return 0;
        }

        return Math.max(0, Math.round((tutar * 100 / matrah) * 10000) / 10000);
    }

    getSatirKonaklamaVergisiTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.calculateRateBasedAmount(this.getSatirMatrah(satir), satir.konaklamaVergisiOrani, satir.konaklamaVergisiTutari);
    }

    previewSatirToplami(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirMatrah(satir) + this.previewNetKdvTutari(satir);
    }

    previewToplamMatrah(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirMatrah(s), 0);
    }

    previewToplamIndirimTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirIndirimTutari(s), 0);
    }

    previewToplamKdv(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.previewKdvTutari(s), 0);
    }

    previewToplamTevkifatTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirTevkifatTutari(s), 0);
    }

    previewToplamNetKdv(): number {
        return this.previewToplamKdv() - this.previewToplamTevkifatTutari();
    }

    previewToplamOtvTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirOtvTutari(s), 0);
    }

    previewToplamOivTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirOivTutari(s), 0);
    }

    previewToplamKonaklamaVergisiTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirKonaklamaVergisiTutari(s), 0);
    }

    previewGenelToplam(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.previewSatirToplami(s), 0);
    }

    getTasinirKartLabel(tasinirKartId?: number | null): string {
        if (!tasinirKartId) {
            return '-';
        }
        return this.tasinirKartSecenekleri().find(x => x.value === tasinirKartId)?.label ?? `#${tasinirKartId}`;
    }

    getDepoLabel(depoId?: number | null): string {
        if (!depoId) {
            return '-';
        }
        return this.depoSecenekleri().find(x => x.value === depoId)?.label ?? `#${depoId}`;
    }

    getSelectedCariDisplay(): string {
        const cari = this.selectedCari();
        return cari ? this.formatCariDisplay(cari) : '-';
    }

    getKdvIstisnaSecenekleri(satir: CreateSatisBelgesiSatiriRequest): Array<{ label: string; value: number }> {
        if (
            satir.kdvUygulamaTipi === KdvUygulamaTipi.Kdvli ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli
        ) {
            return [];
        }

        return this.kdvIstisnaTanimlari()
            .filter(t =>
                t.aktifMi &&
                (this.isAlisMode() ? t.alisIslemlerindeKullanilirMi : t.satisIslemlerindeKullanilirMi) &&
                (t.uygulamaTipi === satir.kdvUygulamaTipi))
            .map(t => ({ label: `${t.kod} - ${t.ad}`, value: t.id }));
    }

    getKdvOraniSecenekleri(currentValue?: number | null): Array<{ label: string; value: number }> {
        const base = [...this.kdvOraniSecenekleri];
        if (currentValue !== null && currentValue !== undefined && !base.some(x => x.value === currentValue)) {
            base.push({ label: `%${currentValue}`, value: currentValue });
        }
        return base;
    }

    getTevkifatSecimi(satir: CreateSatisBelgesiSatiriRequest): string | null {
        if (!satir.tevkifatPay || !satir.tevkifatPayda) {
            return null;
        }
        return `${satir.tevkifatPay}/${satir.tevkifatPayda}`;
    }

    setTevkifatSecimi(satir: CreateSatisBelgesiSatiriRequest, value: string | null): void {
        if (!value) {
            satir.tevkifatPay = null;
            satir.tevkifatPayda = null;
            return;
        }

        const [pay, payda] = value.split('/').map(part => Number(part));
        satir.tevkifatPay = Number.isFinite(pay) ? pay : null;
        satir.tevkifatPayda = Number.isFinite(payda) ? payda : null;
    }

    onSatirKdvTipiChange(satir: CreateSatisBelgesiSatiriRequest, value: KdvUygulamaTipi): void {
        satir.kdvUygulamaTipi = value;
        if (value === KdvUygulamaTipi.Tevkifatli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            this.setSatirParametreGorunurlugu(satir, 'tevkifat', true);
            return;
        }

        satir.tevkifatPay = null;
        satir.tevkifatPayda = null;
        this.setSatirParametreGorunurlugu(satir, 'tevkifat', false);

        if (value === KdvUygulamaTipi.Kdvli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            return;
        }

        satir.kdvOrani = 0;
        satir.kdvIstisnaTanimId = null;
    }

    onSatirTasinirKartChange(satir: CreateSatisBelgesiSatiriRequest, value: number | null): void {
        satir.tasinirKartId = value;
        if (!value) {
            return;
        }

        const kart = this.tasinirKartlar().find(k => k.id === value);
        if (kart && (!satir.birim || this.normalizeLookupValue(satir.birim) === 'adet')) {
            satir.birim = this.getBirimSecenekleri().some(opt => opt.value === kart.birim)
                ? kart.birim
                : this.resolveDefaultBirim();
        }
    }

    // ── Faz 66: Bağlı muhasebe fişi olan belgelerde tüm mutasyon aksiyonları engellenir ──

    canEdit(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak || belge.durum === SatisBelgesiDurumu.Reddedildi;
    }

    canDelete(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canGonder(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canOnayla(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canReddet(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canIptal(belge: SatisBelgesiDto): boolean {
        return belge.durum !== SatisBelgesiDurumu.IptalEdildi &&
            belge.durum !== SatisBelgesiDurumu.FaturaKesildi &&
            belge.durum !== SatisBelgesiDurumu.MusteriyeGonderildi;
    }

    canFisOlustur(belge: SatisBelgesiDto): boolean {
        if (
            belge.durum !== SatisBelgesiDurumu.MuhasebeOnaylandi ||
            belge.muhasebeFisId ||
            belge.belgeTipi === SatisBelgesiTipi.Proforma ||
            belge.belgeTipi === SatisBelgesiTipi.IadeFaturasi
        ) {
            return false;
        }

        if (this.isAlisMode()) {
            return belge.belgeTipi === SatisBelgesiTipi.AlisFaturasi || belge.belgeTipi === SatisBelgesiTipi.AlisIadeFaturasi;
        }

        return belge.belgeTipi === SatisBelgesiTipi.SatisFaturasi ||
            belge.belgeTipi === SatisBelgesiTipi.FaturaTaslagi ||
            belge.belgeTipi === SatisBelgesiTipi.SatisIadeFaturasi;
    }

    hasMuhasebeFisi(belge: SatisBelgesiDto): boolean {
        return !!belge.muhasebeFisId;
    }

    muhasebeFisineGit(belge: SatisBelgesiDto): void {
        if (!belge.muhasebeFisId) {
            return;
        }
        this.router.navigate(['/muhasebe/fisler'], { queryParams: { id: belge.muhasebeFisId } });
    }

    muhasebeFisiOlustur(belge: SatisBelgesiDto): void {
        if (!this.canFisOlustur(belge)) {
            const detail = this.isAlisMode()
                ? (belge.belgeTipi === SatisBelgesiTipi.Proforma
                    ? 'Proforma belgeler için muhasebe fişi oluşturulamaz.'
                    : 'İade faturaları için muhasebe fişi oluşturma henüz desteklenmiyor.')
                : (belge.belgeTipi === SatisBelgesiTipi.Proforma
                    ? 'Proforma belgeler için muhasebe fişi oluşturulamaz.'
                    : 'İade faturaları için muhasebe fişi oluşturma henüz desteklenmiyor.');

            this.messageService.add({
                severity: 'warn',
                summary: 'İşlem Desteklenmiyor',
                detail
            });
            return;
        }

        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" için muhasebe fişi oluşturmak istediğinize emin misiniz?`,
            header: 'Fiş Oluşturma Onayı',
            icon: 'pi pi-file',
            accept: () => {
                this.service.muhasebeFisiOlustur(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Muhasebe fişi oluşturuldu.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Template-safe label/severity helpers (avoids TS indexed-access errors in HTML) ──

    getDurumLabel(durum: SatisBelgesiDurumu): string {
        return this.durumLabels[durum] ?? String(durum);
    }

    getDurumSeverity(durum: SatisBelgesiDurumu): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        return (this.durumSeverities[durum] as 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast') ?? 'info';
    }

    getBelgeTipiLabel(tip: SatisBelgesiTipi): string {
        return this.belgeTipiLabels[tip] ?? String(tip);
    }

    getKaynakModulLabel(modul: SatisKaynakModulu): string {
        return this.kaynakModulLabels[modul] ?? String(modul);
    }

    getSatirTipiLabel(tip: SatisBelgesiSatirTipi): string {
        return this.satirTipiLabels[tip] ?? String(tip);
    }

    getSatirCollapsedSummary(satir: CreateSatisBelgesiSatiriRequest): string {
        const parts: string[] = [];
        const satirTipiLabel = this.getSatirTipiLabel(satir.satirTipi);
        const tasinirLabel = this.getTasinirKartLabel(satir.tasinirKartId);
        const birim = (satir.birim ?? '').trim();
        const miktar = this.normalizeRate(satir.miktar);
        const toplam = this.previewSatirToplami(satir);

        if (satirTipiLabel) {
            parts.push(satirTipiLabel);
        }

        if (tasinirLabel && tasinirLabel !== '-') {
            parts.push(tasinirLabel);
        }

        if (miktar > 0) {
            parts.push(`${miktar.toLocaleString('tr-TR')} ${birim || ''}`.trim());
        }

        parts.push(`${toplam.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₺`);

        return parts.join(' · ');
    }
}

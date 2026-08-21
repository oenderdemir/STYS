import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DialogService, DynamicDialogModule, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { DepolarService } from '../depolar/depolar.service';
import { formatDateForApi, MuhasebeFisDurumlari, parseApiDate } from '../models/muhasebe-fis.model';
import { MuhasebeFisService } from '../services/muhasebe-fis.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import { TasinirMuhasebeFisTaslagiDialogComponent } from '../tasinir-fis-taslagi/tasinir-muhasebe-fis-taslagi-dialog.component';
import { STOK_HAREKET_DURUMLARI, STOK_HAREKET_TIPLERI, STOK_SAYIM_FARKI_YONLERI, StokBakiyeModel, StokDetayModel, StokHareketModel, StokKartOzetModel } from './stok-hareketleri.dto';
import { KdvIstisnaTanimDto, KDV_UYGULAMA_TIPI_SECENEKLERI, KdvUygulamaTipi, KDV_UYGULAMA_TIPI_LABELS } from '../models/kdv-istisna-tanim.model';
import { StokHareketleriService } from './stok-hareketleri.service';

@Component({
    selector: 'app-stok-hareketleri-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        DynamicDialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        ToastModule,
        ToolbarModule,
        TooltipModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './stok-hareketleri.html',
    providers: [MessageService, ConfirmationService, DialogService]
})
export class StokHareketleriPage implements OnInit {
    private readonly service = inject(StokHareketleriService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly muhasebeFisService = inject(MuhasebeFisService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly dialogService = inject(DialogService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    hareketTarihiDate: Date | null = null;

    selectedDepoId?: number;

    records: StokHareketModel[] = [];
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    stokBakiye: StokBakiyeModel[] = [];
    stokKartOzet: StokKartOzetModel[] = [];
    expandedStokDetayKeys: Record<string, boolean> = {};
    model: StokHareketModel = this.createEmpty();

    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    cariKartOptions: Array<{ label: string; value: number }> = [];
    /** Tüm aktif istisna tanımları (cache). */
    private kdvIstisnaTanimAllOptions: Array<{ label: string; value: number }> = [];
    /** Hareket tipi + KDV uygulama tipine göre filtrelenmiş istisna tanımları. */
    filteredKdvIstisnaTanimOptions: Array<{ label: string; value: number }> = [];
    /** Tevkifatlı hariç KDV uygulama tipi seçenekleri. */
    filteredKdvUygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI.filter(s => s.value !== 5);
    readonly kdvOraniSecenekleri = [
        { label: '%0', value: 0 },
        { label: '%1', value: 1 },
        { label: '%8', value: 8 },
        { label: '%10', value: 10 },
        { label: '%18', value: 18 },
        { label: '%20', value: 20 }
    ];

    /** Full TasinirKart models indexed by id for O(1) lookups of tesisId and stokKodu. */
    private tasinirKartByIdMap = new Map<number, TasinirKartModel>();
    private selectedDepoPresetSource: 'none' | 'filter' | 'default' | 'manual' = 'none';
    private lastSelectedTasinirKartId: number | null = null;

    /** Full KdvIstisnaTanim records indexed by id for O(1) lookups. */
    private kdvIstisnaTanimByIdMap = new Map<number, KdvIstisnaTanimDto>();

    /** Active DynamicDialog reference for the muhasebe fiş taslağı dialog. */
    private fisTaslagiDialogRef: DynamicDialogRef | null = null;
    private stokDetayByKey = new Map<string, StokDetayModel>();
    private stokDetayLoadingKeys = new Set<string>();

    readonly hareketTipleri = STOK_HAREKET_TIPLERI;
    readonly durumlar = STOK_HAREKET_DURUMLARI;
    readonly sayimFarkiYonleri = STOK_SAYIM_FARKI_YONLERI;
    readonly kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.pageNumber = 1;
            this.selectedDepoId = undefined;
            this.closeOpenDialogForTesisChange();
            this.loadReferences();
            this.load(1, this.pageSize);
            this.loadSummary();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için stok hareketleri yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
                this.load(1, this.pageSize);
                this.loadSummary();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    loadReferences(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.depoOptions = [];
            this.tasinirKartOptions = [];
            this.cariKartOptions = [];
            this.tasinirKartByIdMap = new Map<number, TasinirKartModel>();
            return;
        }

        this.depolarService.getAll().subscribe({
            next: (items) => {
                this.depoOptions = items
                    .filter((x) => x.aktifMi && (!x.tesisId || x.tesisId === tesisId))
                    .map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
                if (this.selectedDepoId && !this.depoOptions.some((x) => x.value === this.selectedDepoId)) {
                    this.selectedDepoId = undefined;
                }
                this.cdr.detectChanges();
            }
        });
        this.tasinirKartService.getAll().subscribe({
            next: (items) => {
                const aktifler = items.filter((x) => x.aktifMi && (!x.tesisId || x.tesisId === tesisId));
                this.tasinirKartOptions = aktifler.map((x) => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
                this.tasinirKartByIdMap = new Map(aktifler.map((x) => [x.id!, x]));
                this.cdr.detectChanges();
            }
        });
        this.cariKartService.getAll().subscribe({
            next: (items) => {
                this.cariKartOptions = items
                    .filter((x) => !x.tesisId || x.tesisId === tesisId)
                    .map((x) => ({ label: `${x.cariKodu} - ${x.unvanAdSoyad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });
        // Yalnızca aktif istisna tanımlarını yükle; yön/uygulama tipi filtresi anlık yapılır
        this.kdvIstisnaTanimService.filter({ kod: null, ad: null, uygulamaTipi: null, aktifMi: true, satisIslemlerindeKullanilirMi: null, alisIslemlerindeKullanilirMi: null }).subscribe({
            next: (items) => {
                this.kdvIstisnaTanimByIdMap = new Map(items.map((x) => [x.id, x]));
                this.kdvIstisnaTanimAllOptions = items.map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id }));
                this.applyIstisnaFilter();
                this.cdr.detectChanges();
            }
        });
    }

    onDepoFilterChange(): void {
        this.load(1, this.pageSize);
        this.loadSummary();
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, tesisId, this.selectedDepoId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    loadSummary(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.stokBakiye = [];
            this.stokKartOzet = [];
            this.resetStokDetayState();
            return;
        }

        this.resetStokDetayState();

        this.service.getStokBakiye(tesisId, this.selectedDepoId).subscribe({
            next: (items) => {
                this.stokBakiye = items;
                this.cdr.detectChanges();
            }
        });
        this.service.getStokKartOzet(tesisId, this.selectedDepoId).subscribe({
            next: (items) => {
                this.stokKartOzet = items;
                this.cdr.detectChanges();
            }
        });
    }

    toggleStokDetay(row: StokBakiyeModel): void {
        const key = this.getStokDetayKey(row);
        if (this.expandedStokDetayKeys[key]) {
            delete this.expandedStokDetayKeys[key];
            this.expandedStokDetayKeys = { ...this.expandedStokDetayKeys };
            return;
        }

        this.expandedStokDetayKeys = { ...this.expandedStokDetayKeys, [key]: true };
        if (this.stokDetayByKey.has(key) || this.stokDetayLoadingKeys.has(key)) {
            return;
        }

        this.stokDetayLoadingKeys.add(key);
        this.service.getStokDetay(row.depoId, row.tasinirKartId).pipe(finalize(() => {
            this.stokDetayLoadingKeys.delete(key);
            this.cdr.detectChanges();
        })).subscribe({
            next: (detay) => {
                this.stokDetayByKey.set(key, detay);
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                delete this.expandedStokDetayKeys[key];
                this.expandedStokDetayKeys = { ...this.expandedStokDetayKeys };
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    openCreate(): void {
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.hareketTarihiDate = this.startOfNow();
        this.selectedDepoPresetSource = 'none';
        this.lastSelectedTasinirKartId = null;
        if (this.selectedDepoId && this.selectedDepoId > 0) {
            this.model.depoId = this.selectedDepoId;
            this.selectedDepoPresetSource = 'filter';
        }
        this.onHareketTipiChange();
        this.applyIstisnaFilter();
        this.dialogVisible = true;
    }

    openEdit(item: StokHareketModel): void {
        if (item.transferGrupId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Düzenleme Desteklenmiyor', detail: 'Transfer kayıtları doğrudan düzenlenemez. Transferi iptal edip yeniden oluşturunuz.' });
            return;
        }
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.hareketTarihiDate = parseApiDate(item.hareketTarihi);
        this.selectedDepoPresetSource = 'manual';
        this.lastSelectedTasinirKartId = item.tasinirKartId;
        this.applyIstisnaFilter();
        this.dialogVisible = true;
    }

    onTasinirKartChange(tasinirKartId: number | null): void {
        const yeniTasinirKartId = tasinirKartId && tasinirKartId > 0 ? tasinirKartId : null;
        const kartDegisti = this.lastSelectedTasinirKartId !== yeniTasinirKartId;

        this.model.tasinirKartId = yeniTasinirKartId ?? 0;
        this.lastSelectedTasinirKartId = yeniTasinirKartId;

        if (this.dialogMode !== 'create') {
            return;
        }

        if (this.selectedDepoId && this.selectedDepoId > 0) {
            this.model.depoId = this.selectedDepoId;
            this.selectedDepoPresetSource = 'filter';
            return;
        }

        if (!kartDegisti && this.selectedDepoPresetSource === 'manual') {
            return;
        }

        const tasinirKart = yeniTasinirKartId
            ? this.tasinirKartByIdMap.get(yeniTasinirKartId)
            : null;

        const varsayilanDepoId = tasinirKart?.varsayilanDepoId ?? null;
        if (varsayilanDepoId && this.depoOptions.some(x => x.value === varsayilanDepoId)) {
            this.model.depoId = varsayilanDepoId;
            this.selectedDepoPresetSource = 'default';
            return;
        }

        if (this.selectedDepoPresetSource !== 'manual') {
            this.model.depoId = 0;
            this.selectedDepoPresetSource = 'none';
        }
    }

    onDepoChange(depoId: number | null): void {
        this.model.depoId = depoId ?? 0;
        if (this.model.hedefDepoId && this.model.hedefDepoId === this.model.depoId) {
            this.model.hedefDepoId = null;
        }
        if (this.dialogMode === 'create') {
            this.selectedDepoPresetSource = depoId && depoId > 0 ? 'manual' : 'none';
        }
    }

    save(): void {
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }

        if (!this.model.depoId || !this.model.tasinirKartId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Depo ve taşınır kart seçimi zorunludur.' });
            return;
        }

        if (this.isSayimFarki(this.model) && !this.model.sayimFarkiYonu) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Sayım farkı için yön seçimi zorunludur.' });
            return;
        }

        if (this.isTransfer(this.model)) {
            if (!this.model.hedefDepoId) {
                this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Transfer için hedef depo seçimi zorunludur.' });
                return;
            }

            if (this.model.hedefDepoId === this.model.depoId) {
                this.messageService.add({ severity: UiSeverity.Warn, summary: 'Geçersiz Seçim', detail: 'Hedef depo kaynak depo ile aynı olamaz.' });
                return;
            }
        }

        // Client-side KDV validation
        if (!this.isTransfer(this.model) && !this.isSayimFarki(this.model) && this.model.kdvUygulamaTipi === 5) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Desteklenmiyor', detail: 'Tevkifatlı KDV uygulaması henüz desteklenmemektedir.' });
            return;
        }

        if (!this.isTransfer(this.model) && !this.isSayimFarki(this.model) && this.model.kdvUygulamaTipi !== 1 && !this.model.kdvIstisnaTanimId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'KDV\'li dışındaki işlemlerde istisna tanımı seçilmesi zorunludur.' });
            return;
        }

        if (!this.isTransfer(this.model) && !this.isSayimFarki(this.model) && this.model.kdvUygulamaTipi === 1 && this.model.kdvIstisnaTanimId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Geçersiz Seçim', detail: 'KDV\'li işlemlerde istisna tanımı seçilemez.' });
            return;
        }

        if (!this.isTransfer(this.model) && !this.isSayimFarki(this.model) && this.model.kdvUygulamaTipi === 1 && (this.model.kdvOrani == null || this.model.kdvOrani <= 0)) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'KDV\'li işlemlerde KDV oranı 0\'dan büyük olmalıdır.' });
            return;
        }

        if (!this.isTransfer(this.model) && !this.isSayimFarki(this.model) && this.model.kdvUygulamaTipi !== 1 && this.model.kdvOrani !== 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Geçersiz Değer', detail: 'İstisna/kapsam dışı işlemlerde KDV oranı 0 olmalıdır.' });
            return;
        }

        const payload = {
            depoId: this.model.depoId,
            tasinirKartId: this.model.tasinirKartId,
            hareketTarihi: this.formatDateTimeForApi(this.hareketTarihiDate) ?? this.model.hareketTarihi,
            hareketTipi: this.model.hareketTipi,
            miktar: this.model.miktar,
            birimFiyat: this.model.birimFiyat,
            belgeNo: this.model.belgeNo?.trim() || null,
            belgeTarihi: this.model.belgeTarihi || null,
            aciklama: this.model.aciklama?.trim() || null,
            cariKartId: this.model.cariKartId || null,
            kaynakModul: this.model.kaynakModul?.trim() || null,
            kaynakId: this.model.kaynakId || null,
            sayimFarkiYonu: this.isSayimFarki(this.model) ? this.model.sayimFarkiYonu ?? null : null,
            durum: this.model.durum,
            kdvUygulamaTipi: this.model.kdvUygulamaTipi,
            kdvIstisnaTanimId: this.model.kdvIstisnaTanimId ?? null,
            kdvOrani: this.model.kdvOrani
        };

        const transferPayload = {
            kaynakDepoId: this.model.depoId,
            hedefDepoId: this.model.hedefDepoId!,
            tasinirKartId: this.model.tasinirKartId,
            hareketTarihi: this.formatDateTimeForApi(this.hareketTarihiDate) ?? this.model.hareketTarihi,
            miktar: this.model.miktar,
            birimFiyat: this.model.birimFiyat,
            belgeNo: this.model.belgeNo?.trim() || null,
            belgeTarihi: this.model.belgeTarihi || null,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        let request$: Observable<unknown>;
        if (this.dialogMode === 'create' && this.isTransfer(this.model)) {
            request$ = this.service.createTransfer(transferPayload);
        } else if (this.dialogMode === 'edit' && this.model.id) {
            request$ = this.service.update(this.model.id, payload);
        } else {
            request$ = this.service.create(payload);
        }

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.loadSummary();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Kayıt kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: StokHareketModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: item.transferGrupId ? 'Transfer iptal edilsin mi?' : 'Kayıt silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayır',
            accept: () => {
                const request$ = item.transferGrupId ? this.service.transferIptal(item.id!) : this.service.delete(item.id!);
                request$.subscribe({
                    next: () => {
                        this.load();
                        this.loadSummary();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: item.transferGrupId ? 'Transfer iptal edildi.' : 'Kayıt silindi.' });
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    // ──────────────────────────────────────────
    // Muhasebe Fiş Taslağı Oluştur
    // ──────────────────────────────────────────

    /**
     * Determines whether the "Muhasebe Fiş Taslağı Oluştur" button should be enabled for a row.
     * Requires: resolved tesisId > 0, resolved tasinirKodu non-empty, and row.tutar > 0.
     */
    canCreateMuhasebeFisTaslagi(row: StokHareketModel): boolean {
        if (row.transferGrupId || row.hareketTipi === 'Transfer') {
            return false;
        }
        if (!row.tutar || row.tutar <= 0) {
            return false;
        }
        if (!row.tasinirKartId) {
            return false;
        }
        const kart = this.tasinirKartByIdMap.get(row.tasinirKartId);
        if (!kart || !kart.tesisId || kart.tesisId <= 0) {
            return false;
        }
        if (!kart.stokKodu?.trim()) {
            return false;
        }
        return true;
    }

    /**
     * Returns a user-friendly tooltip explaining why the button is disabled,
     * or the enabled tooltip text if the button is active.
     */
    getMuhasebeFisTaslagiTooltip(row: StokHareketModel): string {
        if (row.transferGrupId || row.hareketTipi === 'Transfer') {
            return 'Transfer stok hareketleri için muhasebe fiş taslağı bu fazda desteklenmemektedir.';
        }
        if (!row.tutar || row.tutar <= 0) {
            return 'Tutar sıfır veya negatif olduğu için muhasebe fiş taslağı oluşturulamaz.';
        }
        if (!row.tasinirKartId) {
            return 'Taşınır kart bilgisi bulunamadı.';
        }
        const kart = this.tasinirKartByIdMap.get(row.tasinirKartId);
        if (!kart || !kart.tesisId || kart.tesisId <= 0) {
            return 'Tesis bilgisi bulunamadı.';
        }
        if (!kart.stokKodu?.trim()) {
            return 'Taşınır kodu bulunamadı.';
        }
        return 'Muhasebe fiş taslağı oluştur';
    }

    /**
     * Opens the TasinirMuhasebeFisTaslagiDialogComponent as a DynamicDialog with
     * pre-filled data from the selected stok hareket row.
     *
     * Before opening the dialog, checks via getByKaynak whether an active
     * (non-İptal) fiş already exists for this source operation.
     */
    muhasebeFisTaslagiOlustur(row: StokHareketModel): void {
        if (this.fisTaslagiDialogRef) {
            return;
        }

        const kart = this.tasinirKartByIdMap.get(row.tasinirKartId);
        if (!kart) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Taşınır kart bilgisi bulunamadı.'
            });
            return;
        }

        const tesisId = kart.tesisId ?? 0;
        const tasinirKodu = kart.stokKodu?.trim() ?? '';

        if (tesisId <= 0 || !tasinirKodu) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Tesis veya taşınır kodu bilgisi eksik.'
            });
            return;
        }

        // Frontend pre-check: bu kaynaktan (StokHareket) zaten aktif bir fiş var mı?
        this.muhasebeFisService.getByKaynak('StokHareket', row.id!).subscribe({
            next: (fisler) => {
                const aktifFis = fisler.find(f => f.durum !== MuhasebeFisDurumlari.Iptal);
                if (aktifFis) {
                    this.messageService.add({
                        severity: UiSeverity.Warn,
                        summary: 'Fiş Zaten Mevcut',
                        detail: `Bu stok hareketi için zaten bir muhasebe fişi oluşturulmuş. ` +
                            `Mevcut fiş: ${aktifFis.fisNo} (Durum: ${aktifFis.durum}). ` +
                            `Yeni fiş oluşturmak için önce mevcut fişi iptal ediniz.`
                    });
                    return;
                }

                this._openFisTaslagiDialog(tesisId, tasinirKodu, row);
            },
            error: () => {
                // getByKaynak başarısız olsa bile dialog'u aç (backend zaten korumalı)
                this._openFisTaslagiDialog(tesisId, tasinirKodu, row);
            }
        });
    }

    /** Internal helper to open the dialog after pre-checks pass. */
    private _openFisTaslagiDialog(tesisId: number, tasinirKodu: string, row: StokHareketModel): void {
        if (this.fisTaslagiDialogRef) {
            return;
        }

        this.fisTaslagiDialogRef = this.dialogService.open(TasinirMuhasebeFisTaslagiDialogComponent, {
            header: 'Stok Hareketinden Muhasebe Fiş Taslağı Oluştur',
            width: '900px',
            modal: true,
            closable: true,
            dismissableMask: false,
            data: {
                tesisId,
                tasinirKodu,
                tutar: row.tutar,
                referansTipi: 'StokHareket',
                referansId: String(row.id ?? ''),
                belgeNo: row.belgeNo?.trim() || null,
                aciklama: row.aciklama?.trim() || null,
                kdvUygulamaTipi: row.kdvUygulamaTipi,
                kdvIstisnaKodu: row.kdvIstisnaKodu,
                kdvIstisnaAciklamasi: row.kdvIstisnaAciklamasi,
                hareketTipi: row.hareketTipi,
                kdvTutari: row.kdvTutari
            }
        });

        this.fisTaslagiDialogRef?.onClose.subscribe(() => {
            this.fisTaslagiDialogRef = null;
        });
    }

    private closeOpenDialogForTesisChange(): void {
        if (this.dialogVisible) {
            this.dialogVisible = false;
            this.model = this.createEmpty();
        }

        if (this.fisTaslagiDialogRef) {
            this.fisTaslagiDialogRef.close();
            this.fisTaslagiDialogRef = null;
        }
    }

    // ──────────────────────────────────────────

    private createEmpty(): StokHareketModel {
        const now = this.startOfNow();
        return {
            depoId: 0,
            tasinirKartId: 0,
            hareketTarihi: this.formatDateTimeForApi(now) ?? '',
            hareketTipi: 'Giris',
            hedefDepoId: null,
            miktar: 1,
            birimFiyat: 0,
            tutar: 0,
            belgeNo: null,
            belgeTarihi: null,
            aciklama: null,
            cariKartId: null,
            kaynakModul: null,
            kaynakId: null,
            transferGrupId: null,
            transferYonu: null,
            sayimFarkiYonu: null,
            karsiDepoId: null,
            durum: 'Aktif',
            kdvUygulamaTipi: 1,
            kdvIstisnaTanimId: null,
            kdvIstisnaKodu: null,
            kdvIstisnaAciklamasi: null,
            kdvOrani: 20,
            kdvTutari: 0
        };
    }

    private startOfNow(): Date {
        return new Date();
    }

    private formatDateTimeForApi(value: Date | string | null | undefined): string | null {
        const date = value instanceof Date ? value : parseApiDate(value);
        if (!date) {
            return null;
        }

        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        const second = String(date.getSeconds()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}:${second}`;
    }

    private getSeciliTesisIdOrWarn(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe işlemi için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    /** Resolve KDV uygulama tipi label for display in table. */
    getKdvUygulamaTipiLabel(tip: number): string {
        return this.kdvUygulamaTipiLabels[tip as KdvUygulamaTipi] ?? 'KDV\'li';
    }

    /** Çıkış etkisi olan hareket tipleri (backend StokHareketTipleri.CikisEtkisi ile aynı). */
    private static readonly CIKIS_ETKISI = new Set<string>(['Cikis', 'Transfer', 'Sarf', 'Zimmet']);

    /**
     * Hareket tipine göre işlem yönünü belirle:
     * Çıkış etkisi olanlar → Satış, diğerleri → Alış.
     */
    private getIslemYonu(): 'Satis' | 'Alis' {
        if (this.model.hareketTipi === 'Transfer') {
            return this.model.transferYonu === 'Giris' ? 'Alis' : 'Satis';
        }
        if (this.isSayimFarki(this.model)) {
            return this.model.sayimFarkiYonu === 'Eksik' ? 'Satis' : 'Alis';
        }
        return StokHareketleriPage.CIKIS_ETKISI.has(this.model.hareketTipi) ? 'Satis' : 'Alis';
    }

    /** Hareket tipi değiştiğinde istisna filtresini yeniden uygula. */
    onHareketTipiChange(): void {
        if (this.isTransfer(this.model)) {
            this.model.cariKartId = null;
            this.model.kdvUygulamaTipi = 4;
            this.model.kdvIstisnaTanimId = null;
            this.model.kdvIstisnaKodu = null;
            this.model.kdvIstisnaAciklamasi = null;
            this.model.kdvOrani = 0;
            this.model.kdvTutari = 0;
        } else if (this.isSayimFarki(this.model)) {
            this.model.kdvUygulamaTipi = 4;
            this.model.kdvIstisnaTanimId = null;
            this.model.kdvIstisnaKodu = null;
            this.model.kdvIstisnaAciklamasi = null;
            this.model.kdvOrani = 0;
            this.model.kdvTutari = 0;
        } else if (this.dialogMode === 'create' && this.model.kdvUygulamaTipi === 4 && this.model.kdvOrani === 0) {
            this.model.kdvUygulamaTipi = 1;
            this.model.kdvOrani = 20;
        }

        if (!this.isTransfer(this.model)) {
            this.model.hedefDepoId = null;
        }
        if (!this.isSayimFarki(this.model)) {
            this.model.sayimFarkiYonu = null;
        } else if (!this.model.sayimFarkiYonu) {
            this.model.sayimFarkiYonu = 'Fazla';
        }
        this.applyIstisnaFilter();
    }

    isSayimFarki(row: Pick<StokHareketModel, 'hareketTipi'>): boolean {
        return row.hareketTipi === 'SayimFarki';
    }

    isTransfer(row: Pick<StokHareketModel, 'hareketTipi'>): boolean {
        return row.hareketTipi === 'Transfer';
    }

    showHedefDepoField(): boolean {
        return this.isTransfer(this.model);
    }

    showDurumField(): boolean {
        return !this.showHedefDepoField();
    }

    isStokDetayExpanded(row: StokBakiyeModel): boolean {
        return !!this.expandedStokDetayKeys[this.getStokDetayKey(row)];
    }

    getStokDetay(row: StokBakiyeModel): StokDetayModel | undefined {
        return this.stokDetayByKey.get(this.getStokDetayKey(row));
    }

    isStokDetayLoading(row: StokBakiyeModel): boolean {
        return this.stokDetayLoadingKeys.has(this.getStokDetayKey(row));
    }

    getStokDetayToggleIcon(row: StokBakiyeModel): string {
        return this.isStokDetayExpanded(row) ? 'pi pi-chevron-down' : 'pi pi-chevron-right';
    }

    getStokDetayModuLabel(malzemeKayitTipi: string | null | undefined): string {
        switch (malzemeKayitTipi) {
            case 'MalzemeleriAyriKayittaTut':
                return 'Malzemeleri Ayrı Kayıtta Tut';
            case 'FiyatFarkliMalzemeleriAyriKayittaTut':
                return 'Fiyatı Farklı Malzemeleri Ayrı Kayıtta Tut';
            case 'MalzemeleriAyniKayittaTut':
                return 'Malzemeleri Aynı Kayıtta Tut';
            default:
                return malzemeKayitTipi ?? '-';
        }
    }

    getHedefDepoOptions(): Array<{ label: string; value: number }> {
        return this.depoOptions.filter(x => x.value !== this.model.depoId);
    }

    getDepoLabel(depoId: number | null | undefined): string {
        if (!depoId) {
            return '-';
        }

        return this.depoOptions.find(x => x.value === depoId)?.label ?? String(depoId);
    }

    getTasinirKartLabel(tasinirKartId: number): string {
        return this.tasinirKartOptions.find(x => x.value === tasinirKartId)?.label ?? String(tasinirKartId);
    }

    getHareketTipiLabel(row: StokHareketModel): string {
        if (row.hareketTipi === 'SayimFarki') {
            return row.sayimFarkiYonu === 'Eksik'
                ? 'Sayım Farkı / Stok Eksiği'
                : 'Sayım Farkı / Stok Fazlası';
        }

        if (row.hareketTipi !== 'Transfer') {
            return row.hareketTipi;
        }

        return row.transferYonu === 'Giris' ? 'Transfer / Giriş' : 'Transfer / Çıkış';
    }

    getTransferRotaLabel(row: StokHareketModel): string | null {
        if (row.hareketTipi !== 'Transfer' || !row.karsiDepoId) {
            return null;
        }

        if (row.transferYonu === 'Giris') {
            return `${this.getDepoLabel(row.karsiDepoId)} → ${this.getDepoLabel(row.depoId)}`;
        }

        return `${this.getDepoLabel(row.depoId)} → ${this.getDepoLabel(row.karsiDepoId)}`;
    }

    /** Called when KDV uygulama tipi changes in the dialog. Clears istisna selection if Kdvli. */
    onKdvUygulamaTipiChange(): void {
        if (this.model.kdvUygulamaTipi === 1) {
            this.model.kdvIstisnaTanimId = null;
            this.model.kdvIstisnaKodu = null;
            this.model.kdvIstisnaAciklamasi = null;
            this.model.kdvTutari = 0;
        } else {
            this.model.kdvOrani = 0;
            this.model.kdvTutari = 0;
        }
        this.applyIstisnaFilter();
    }

    getKdvOraniSecenekleri(currentValue?: number | null): Array<{ label: string; value: number }> {
        const base = [...this.kdvOraniSecenekleri];
        if (currentValue !== null && currentValue !== undefined && !base.some(x => x.value === currentValue)) {
            base.push({ label: `%${currentValue}`, value: currentValue });
        }
        return base;
    }

    /**
     * İstisna tanımı listesini; hareket tipi (işlem yönü) ve seçili KDV uygulama tipine göre filtreler.
     * Yalnızca aktif (AktifMi = true) ve seçili yönde kullanılabilir tanımlar gösterilir.
     */
    private applyIstisnaFilter(): void {
        const islemYonu = this.getIslemYonu();
        const seciliTip = this.model.kdvUygulamaTipi;

        let filteredIds: Set<number>;

        if (seciliTip === 1) {
            // KDV'li → istisna listesi boş
            filteredIds = new Set();
        } else {
            filteredIds = new Set<number>();
            for (const [id, tanim] of this.kdvIstisnaTanimByIdMap) {
                // Aktif olmalı
                if (!tanim.aktifMi) continue;
                // Uygulama tipi eşleşmeli
                if (tanim.uygulamaTipi !== seciliTip) continue;
                // İşlem yönüne uygun olmalı
                if (islemYonu === 'Satis' && !tanim.satisIslemlerindeKullanilirMi) continue;
                if (islemYonu === 'Alis' && !tanim.alisIslemlerindeKullanilirMi) continue;
                filteredIds.add(id);
            }
        }

        this.filteredKdvIstisnaTanimOptions = this.kdvIstisnaTanimAllOptions.filter(o => filteredIds.has(o.value));

        // Eğer mevcut seçili istisna artık listede yoksa temizle
        if (this.model.kdvIstisnaTanimId && !filteredIds.has(this.model.kdvIstisnaTanimId)) {
            this.model.kdvIstisnaTanimId = null;
            this.model.kdvIstisnaKodu = null;
            this.model.kdvIstisnaAciklamasi = null;
        }
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }

    private getStokDetayKey(row: Pick<StokBakiyeModel, 'depoId' | 'tasinirKartId'>): string {
        return `${row.depoId}:${row.tasinirKartId}`;
    }

    private resetStokDetayState(): void {
        this.expandedStokDetayKeys = {};
        this.stokDetayByKey.clear();
        this.stokDetayLoadingKeys.clear();
    }
}

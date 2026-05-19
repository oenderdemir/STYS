import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
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
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { DepolarService } from '../depolar/depolar.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { TasinirMuhasebeFisTaslagiDialogComponent } from '../tasinir-fis-taslagi/tasinir-muhasebe-fis-taslagi-dialog.component';
import { STOK_HAREKET_DURUMLARI, STOK_HAREKET_TIPLERI, StokBakiyeModel, StokHareketModel, StokKartOzetModel } from './stok-hareketleri.dto';
import { StokHareketleriService } from './stok-hareketleri.service';

@Component({
    selector: 'app-stok-hareketleri-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DialogModule,
        DynamicDialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        ToastModule,
        ToolbarModule,
        TooltipModule
    ],
    templateUrl: './stok-hareketleri.html',
    providers: [MessageService, ConfirmationService, DialogService]
})
export class StokHareketleriPage implements OnInit {
    private readonly service = inject(StokHareketleriService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly dialogService = inject(DialogService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    selectedDepoId?: number;

    records: StokHareketModel[] = [];
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    stokBakiye: StokBakiyeModel[] = [];
    stokKartOzet: StokKartOzetModel[] = [];
    model: StokHareketModel = this.createEmpty();

    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    cariKartOptions: Array<{ label: string; value: number }> = [];

    /** Full TasinirKart models indexed by id for O(1) lookups of tesisId and stokKodu. */
    private tasinirKartByIdMap = new Map<number, TasinirKartModel>();

    /** Active DynamicDialog reference for the muhasebe fiş taslağı dialog. */
    private fisTaslagiDialogRef: DynamicDialogRef | null = null;

    readonly hareketTipleri = STOK_HAREKET_TIPLERI;
    readonly durumlar = STOK_HAREKET_DURUMLARI;

    ngOnInit(): void {
        this.loadReferences();
        this.load(1, this.pageSize);
        this.loadSummary();
    }

    loadReferences(): void {
        this.depolarService.getAll().subscribe({
            next: (items) => {
                this.depoOptions = items.filter((x) => x.aktifMi).map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });
        this.tasinirKartService.getAll().subscribe({
            next: (items) => {
                const aktifler = items.filter((x) => x.aktifMi);
                this.tasinirKartOptions = aktifler.map((x) => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
                this.tasinirKartByIdMap = new Map(aktifler.map((x) => [x.id!, x]));
                this.cdr.detectChanges();
            }
        });
        this.cariKartService.getAll().subscribe({
            next: (items) => {
                this.cariKartOptions = items.map((x) => ({ label: `${x.cariKodu} - ${x.unvanAdSoyad}`, value: x.id! }));
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
        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.selectedDepoId).pipe(finalize(() => {
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
        this.service.getStokBakiye(this.selectedDepoId).subscribe({
            next: (items) => {
                this.stokBakiye = items;
                this.cdr.detectChanges();
            }
        });
        this.service.getStokKartOzet(this.selectedDepoId).subscribe({
            next: (items) => {
                this.stokKartOzet = items;
                this.cdr.detectChanges();
            }
        });
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        if (this.selectedDepoId && this.selectedDepoId > 0) {
            this.model.depoId = this.selectedDepoId;
        }
        this.dialogVisible = true;
    }

    openEdit(item: StokHareketModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.depoId || !this.model.tasinirKartId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Depo ve taşınır kart seçimi zorunludur.' });
            return;
        }

        const payload = {
            depoId: this.model.depoId,
            tasinirKartId: this.model.tasinirKartId,
            hareketTarihi: this.model.hareketTarihi,
            hareketTipi: this.model.hareketTipi,
            miktar: this.model.miktar,
            birimFiyat: this.model.birimFiyat,
            belgeNo: this.model.belgeNo?.trim() || null,
            belgeTarihi: this.model.belgeTarihi || null,
            aciklama: this.model.aciklama?.trim() || null,
            cariKartId: this.model.cariKartId || null,
            kaynakModul: this.model.kaynakModul?.trim() || null,
            kaynakId: this.model.kaynakId || null,
            durum: this.model.durum
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload)
            : this.service.create(payload);

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
            message: 'Kayıt silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayır',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.loadSummary();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Kayıt silindi.' });
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
                aciklama: row.aciklama?.trim() || null
            }
        });

        this.fisTaslagiDialogRef?.onClose.subscribe(() => {
            this.fisTaslagiDialogRef = null;
        });
    }

    // ──────────────────────────────────────────

    private createEmpty(): StokHareketModel {
        return {
            depoId: 0,
            tasinirKartId: 0,
            hareketTarihi: new Date().toISOString(),
            hareketTipi: 'Giriş',
            miktar: 1,
            birimFiyat: 0,
            tutar: 0,
            belgeNo: null,
            belgeTarihi: null,
            aciklama: null,
            cariKartId: null,
            kaynakModul: null,
            kaynakId: null,
            durum: 'Aktif'
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { DepolarService } from '../depolar/depolar.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { STOK_HAREKET_DURUMLARI, STOK_HAREKET_TIPLERI, StokBakiyeModel, StokHareketModel, StokKartOzetModel } from './stok-hareketleri.dto';
import { StokHareketleriService } from './stok-hareketleri.service';

@Component({
    selector: 'app-stok-hareketleri-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TabsModule, ToastModule, ToolbarModule],
    templateUrl: './stok-hareketleri.html',
    providers: [MessageService, ConfirmationService]
})
export class StokHareketleriPage implements OnInit {
    private readonly service = inject(StokHareketleriService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
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
                this.tasinirKartOptions = items.filter((x) => x.aktifMi).map((x) => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
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
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Depo ve tasinir kart secimi zorunludur.' });
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
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: StokHareketModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Kayit silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.loadSummary();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    private createEmpty(): StokHareketModel {
        return {
            depoId: 0,
            tasinirKartId: 0,
            hareketTarihi: new Date().toISOString(),
            hareketTipi: 'Giris',
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
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepoModel, MuhasebeTesisModel } from './depolar.dto';
import { DepolarService } from './depolar.service';

@Component({
    selector: 'app-depolar-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './depolar.html',
    providers: [MessageService, ConfirmationService]
})
export class DepolarPage implements OnInit {
    private readonly service = inject(DepolarService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: DepoModel[] = [];
    filteredRecords: DepoModel[] = [];
    model: DepoModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    tesisler: MuhasebeTesisModel[] = [];
    tesisSecenekleri: Array<{ label: string; value: number | null }> = [];
    selectedTesisId: number | null = null;

    ngOnInit(): void {
        this.loadTesisler();
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.selectedTesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.applyClientFilter();
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

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = this.selectedTesisId;
        this.dialogVisible = true;
    }

    openEdit(item: DepoModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.kod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kod ve ad zorunludur.' });
            return;
        }

        this.saving = true;
        const payload = {
            tesisId: this.model.tesisId ?? null,
            kod: this.model.kod.trim(),
            ad: this.model.ad.trim(),
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload)
            : this.service.create(payload);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: DepoModel): void {
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
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    onTesisFilterChange(): void {
        this.pageNumber = 1;
        this.load(1, this.pageSize);
    }

    getTesisAdi(tesisId?: number | null): string {
        if (!tesisId) {
            return '-';
        }
        return this.tesisler.find((x) => x.id === tesisId)?.ad ?? `#${tesisId}`;
    }

    private createEmpty(): DepoModel {
        return {
            tesisId: null,
            kod: '',
            ad: '',
            aktifMi: true,
            aciklama: null
        };
    }

    private applyClientFilter(): void {
        if (!this.selectedTesisId) {
            this.filteredRecords = [...this.records];
            return;
        }

        this.filteredRecords = this.records.filter((x) => x.tesisId === this.selectedTesisId);
    }

    private loadTesisler(): void {
        this.service.getTesisler().subscribe({
            next: (items) => {
                this.tesisler = [...items].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? ''));
                this.tesisSecenekleri = [{ label: 'Tum Tesisler', value: null }, ...this.tesisler.map((x) => ({ label: x.ad, value: x.id }))];
                if (!this.selectedTesisId && this.tesisler.length > 0) {
                    this.selectedTesisId = this.tesisler[0].id;
                }
                this.load(1, this.pageSize);
            },
            error: (error: unknown) => {
                this.showError(error);
                this.load(1, this.pageSize);
            }
        });
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

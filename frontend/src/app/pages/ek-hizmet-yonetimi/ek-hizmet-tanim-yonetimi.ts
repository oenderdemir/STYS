import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, forkJoin, Observable, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KonaklamaTipiIcerikHizmetSecenekleri } from '../konaklama-tipi-yonetimi/konaklama-tipi-icerik.constants';
import { EkHizmetTanimFormRow, GlobalEkHizmetTanimiDto } from './ek-hizmet-yonetimi.dto';
import { EkHizmetYonetimiService } from './ek-hizmet-yonetimi.service';

@Component({
    selector: 'app-ek-hizmet-tanim-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, ConfirmDialogModule, SelectModule, InputTextModule, TableModule, TextareaModule, ToastModule, ToolbarModule],
    templateUrl: './ek-hizmet-tanim-yonetimi.html',
    styles: [`
        .page-shell { display: flex; flex-direction: column; gap: 1.25rem; }
        .page-card { background: #fff; border: 1px solid #dbe4ee; border-radius: 1rem; padding: 1.25rem; box-shadow: 0 12px 30px rgba(15, 23, 42, 0.04); }
        .page-title { margin: 0; font-size: 1.1rem; font-weight: 700; color: #0f172a; }
        .page-subtitle { margin-top: 0.35rem; color: #64748b; font-size: 0.92rem; line-height: 1.45; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-wrapper { overflow: auto; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-table { width: 100%; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-thead > tr > th { white-space: nowrap; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-tbody > tr > td { vertical-align: top; }
        .cell-input { width: 100%; }
        .toolbar-links { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    `],
    providers: [MessageService, ConfirmationService]
})
export class EkHizmetTanimYonetimi implements OnInit {
    private readonly service = inject(EkHizmetYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    rows: EkHizmetTanimFormRow[] = [];
    loading = false;
    saving = false;
    private deletedIds = new Set<number>();

    readonly paketIcerikSecenekleri = [{ label: 'Eslesme Yok', value: null as string | null }, ...KonaklamaTipiIcerikHizmetSecenekleri];

    get canManage(): boolean {
        return this.authService.hasPermission('EkHizmetTanimYonetimi.Manage');
    }

    get canViewAssignments(): boolean {
        return this.authService.hasPermission('EkHizmetTesisAtamaYonetimi.View');
    }

    get canViewTariffs(): boolean {
        return this.authService.hasPermission('EkHizmetTarifeYonetimi.View');
    }

    ngOnInit(): void {
        this.loadRows();
    }

    refresh(): void {
        this.loadRows();
    }

    openAssignments(): void {
        void this.router.navigate(['/ek-hizmet-atamalari']);
    }

    openTariffs(): void {
        void this.router.navigate(['/ek-hizmet-tarifeleri']);
    }

    addRow(): void {
        if (!this.canManage) {
            return;
        }

        this.rows = [
            ...this.rows,
            {
                ad: '',
                aciklama: null,
                birimAdi: 'Adet',
                paketIcerikHizmetKodu: null,
                aktifMi: true
            }
        ];
    }

    removeRow(index: number): void {
        if (!this.canManage) {
            return;
        }

        const row = this.rows[index];
        if (row?.id) {
            this.deletedIds.add(row.id);
        }

        this.rows = this.rows.filter((_, i) => i !== index);
    }

    save(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const validationError = this.validateRows();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const operations: Observable<unknown>[] = [];
        for (const id of this.deletedIds) {
            operations.push(this.service.deleteGlobalTanim(id));
        }

        for (const row of this.rows) {
            const payload = this.toDto(row);
            operations.push(row.id ? this.service.updateGlobalTanim(row.id, payload) : this.service.createGlobalTanim(payload));
        }

        this.saving = true;
        (operations.length > 0 ? forkJoin(operations) : of([]))
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Global ek hizmet tanimlari kaydedildi.' });
                    this.loadRows();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    confirmDeletePersisted(index: number): void {
        const row = this.rows[index];
        if (!this.canManage || !row) {
            return;
        }

        this.confirmationService.confirm({
            message: row.id ? `"${row.ad || 'Kayit'}" satirini kaldirmak istediginize emin misiniz?` : 'Bu satiri kaldirmak istediginize emin misiniz?',
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => this.removeRow(index)
        });
    }

    trackByIndex(index: number): number {
        return index;
    }

    private loadRows(): void {
        this.loading = true;
        this.service
            .getGlobalTanimlar()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.rows = items.map((item) => this.toFormRow(item));
                    this.deletedIds.clear();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.rows = [];
                    this.deletedIds.clear();
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private toFormRow(item: GlobalEkHizmetTanimiDto): EkHizmetTanimFormRow {
        return {
            id: item.id ?? null,
            ad: item.ad,
            aciklama: item.aciklama,
            birimAdi: item.birimAdi,
            paketIcerikHizmetKodu: item.paketIcerikHizmetKodu,
            aktifMi: item.aktifMi
        };
    }

    private toDto(row: EkHizmetTanimFormRow): GlobalEkHizmetTanimiDto {
        return {
            id: row.id ?? null,
            ad: row.ad.trim(),
            aciklama: row.aciklama?.trim() || null,
            birimAdi: row.birimAdi.trim(),
            paketIcerikHizmetKodu: row.paketIcerikHizmetKodu,
            aktifMi: row.aktifMi
        };
    }

    private validateRows(): string | null {
        if (this.rows.length === 0 && this.deletedIds.size === 0) {
            return 'Kaydedilecek degisiklik bulunmuyor.';
        }

        const seen = new Set<string>();
        for (let i = 0; i < this.rows.length; i++) {
            const row = this.rows[i];
            const lineNo = i + 1;
            if (!row.ad?.trim()) {
                return `${lineNo}. satir: Hizmet adi zorunludur.`;
            }

            if (!row.birimAdi?.trim()) {
                return `${lineNo}. satir: Birim adi zorunludur.`;
            }

            const key = row.ad.trim().toLocaleLowerCase('tr-TR');
            if (seen.has(key)) {
                return `${lineNo}. satir: Ayni isimle birden fazla global tanim olamaz.`;
            }

            seen.add(key);
        }

        return null;
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

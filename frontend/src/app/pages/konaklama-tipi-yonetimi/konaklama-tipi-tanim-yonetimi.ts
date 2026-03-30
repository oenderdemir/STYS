import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KonaklamaTipiDialog } from './konaklama-tipi-dialog';
import { KonaklamaTipiDto } from './konaklama-tipi-yonetimi.dto';
import { KonaklamaTipiYonetimiService } from './konaklama-tipi-yonetimi.service';

@Component({
    selector: 'app-konaklama-tipi-tanim-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule, KonaklamaTipiDialog],
    templateUrl: './konaklama-tipi-tanim-yonetimi.html',
    styles: [`
        .page-shell {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .page-card {
            background: #ffffff;
            border: 1px solid #dbe4ee;
            border-radius: 1rem;
            padding: 1.25rem;
            box-shadow: 0 12px 30px rgba(15, 23, 42, 0.04);
        }

        .section-title {
            margin: 0;
            font-size: 1.1rem;
            font-weight: 700;
            color: #0f172a;
        }

        .section-subtitle {
            margin-top: 0.35rem;
            color: #64748b;
            font-size: 0.92rem;
            line-height: 1.45;
            max-width: 48rem;
        }

        .caption-tools {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 1rem;
            margin-bottom: 1rem;
        }

        .search-box {
            min-width: 16rem;
        }

        :host ::ng-deep .konaklama-table .p-datatable-wrapper {
            overflow: auto;
        }

        :host ::ng-deep .konaklama-table .p-datatable-table {
            width: 100%;
        }

        :host ::ng-deep .konaklama-table .p-datatable-thead > tr > th {
            white-space: nowrap;
        }

        :host ::ng-deep .konaklama-table .p-datatable-tbody > tr > td {
            vertical-align: top;
        }

        @media (max-width: 991px) {
            .caption-tools {
                flex-direction: column;
                align-items: stretch;
            }

            .search-box {
                min-width: 0;
                width: 100%;
            }
        }
    `],
    providers: [MessageService, ConfirmationService]
})
export class KonaklamaTipiTanimYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(KonaklamaTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    konaklamaTipleri: KonaklamaTipiDto[] = [];
    selectedKonaklamaTipi: KonaklamaTipiDto = this.getEmptyModel();

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    sortBy = 'ad';
    sortDir: SortDirection = 'asc';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManageGlobal(): boolean {
        return this.hasAnyPermission('KonaklamaTipiTanimYonetimi.Manage', 'KonaklamaTipiYonetimi.Manage');
    }

    get canViewAssignments(): boolean {
        return this.hasAnyPermission('KonaklamaTipiTesisAtamaYonetimi.View', 'KonaklamaTipiYonetimi.View');
    }

    ngOnInit(): void {
        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
    }

    ngOnDestroy(): void {
        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
            this.searchDebounceHandle = null;
        }
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        const sort = resolveSortFromLazyPayload(event, this.sortBy, this.sortDir);
        if (this.loading && nextPageNumber === this.pageNumber && nextPageSize === this.pageSize && sort.sortBy === this.sortBy && sort.sortDir === this.sortDir) {
            return;
        }

        this.pageNumber = nextPageNumber;
        this.pageSize = nextPageSize;
        this.sortBy = sort.sortBy;
        this.sortDir = sort.sortDir;
        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
    }

    openAssignments(): void {
        void this.router.navigate(['/konaklama-tipi-atamalari']);
    }

    openNew(): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.selectedKonaklamaTipi = this.getEmptyModel();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: KonaklamaTipiDto): void {
        if (!this.canManageGlobal) {
            return;
        }
        this.openDialogWithDetail(item.id ?? null, 'edit');
    }

    openView(item: KonaklamaTipiDto): void {
        this.openDialogWithDetail(item.id ?? null, 'view');
    }

    onDialogSave(payload: KonaklamaTipiDto): void {
        if (this.saving || !this.canManageGlobal) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedKonaklamaTipi.id
                ? this.service.updateKonaklamaTipi(this.selectedKonaklamaTipi.id, payload)
                : this.service.createKonaklamaTipi(payload);

        this.saving = true;
        save$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Konaklama tipi guncellendi.' : 'Konaklama tipi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteKonaklamaTipi(item: KonaklamaTipiDto): void {
        if (!this.canManageGlobal || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteKonaklamaTipi(item.id!).subscribe({
                    next: () => {
                        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklama tipi silindi.' });
                        this.cdr.detectChanges();
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                        this.cdr.detectChanges();
                    }
                });
            }
        });
    }

    private loadKonaklamaTipleri(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getKonaklamaTipleriPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.konaklamaTipleri = pagedResponse.items;
                    this.pageNumber = pagedResponse.pageNumber;
                    this.pageSize = pagedResponse.pageSize;
                    this.totalRecords = pagedResponse.totalCount;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private openDialogWithDetail(id: number | null, mode: CrudDialogMode): void {
        if (!id) {
            return;
        }

        this.saving = true;
        this.service
            .getKonaklamaTipiById(id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (item) => {
                    this.selectedKonaklamaTipi = item;
                    this.dialogMode = mode;
                    this.dialogVisible = true;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private hasAnyPermission(...permissions: string[]): boolean {
        return permissions.some((permission) => this.authService.hasPermission(permission));
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

    private getEmptyModel(): KonaklamaTipiDto {
        return {
            kod: '',
            ad: '',
            aktifMi: true,
            icerikKalemleri: []
        };
    }
}

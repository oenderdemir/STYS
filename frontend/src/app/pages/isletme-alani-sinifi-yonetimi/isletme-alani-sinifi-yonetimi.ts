import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { IsletmeAlaniSinifiDialog } from './isletme-alani-sinifi-dialog';
import { IsletmeAlaniYonetimiService } from '../isletme-alani-yonetimi/isletme-alani-yonetimi.service';
import { IsletmeAlaniSinifiDto } from '../isletme-alani-yonetimi/isletme-alani-yonetimi.dto';

@Component({
    selector: 'app-isletme-alani-sinifi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, IsletmeAlaniSinifiDialog],
    templateUrl: './isletme-alani-sinifi-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IsletmeAlaniSinifiYonetimi implements OnDestroy {
    private readonly service = inject(IsletmeAlaniYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    siniflar: IsletmeAlaniSinifiDto[] = [];
    selectedSinif: IsletmeAlaniSinifiDto = this.getEmptySinif();
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

    get canManage(): boolean {
        return this.authService.hasPermission('IsletmeAlaniYonetimi.Manage');
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
        this.loadSiniflar(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadSiniflar(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadSiniflar(this.pageNumber, this.pageSize);
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedSinif = this.getEmptySinif();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(sinif: IsletmeAlaniSinifiDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedSinif = { ...sinif };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(sinif: IsletmeAlaniSinifiDto): void {
        this.selectedSinif = { ...sinif };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: IsletmeAlaniSinifiDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedSinif.id
            ? this.service.updateSinif(this.selectedSinif.id, payload)
            : this.service.createSinif(payload);

        this.saving = true;
        save$
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadSiniflar(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Isletme alani sinifi guncellendi.' : 'Isletme alani sinifi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteSinif(sinif: IsletmeAlaniSinifiDto): void {
        if (!this.canManage || !sinif.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${sinif.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteSinif(sinif.id!).subscribe({
                    next: () => {
                        this.loadSiniflar(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Isletme alani sinifi silindi.' });
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

    private loadSiniflar(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getSiniflarPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadSiniflar(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.siniflar = pagedResponse.items;
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

    private getEmptySinif(): IsletmeAlaniSinifiDto {
        return {
            kod: '',
            ad: '',
            aktifMi: true
        };
    }
}

import { UiSeverity } from '@/app/core/ui/ui-severity.constants';

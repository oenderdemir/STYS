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
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { IlDialog } from './il-dialog';
import { IlDto } from './il-yonetimi.dto';
import { IlYonetimiService } from './il-yonetimi.service';

@Component({
    selector: 'app-il-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule, IlDialog],
    templateUrl: './il-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IlYonetimi implements OnDestroy {
    private readonly service = inject(IlYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    iller: IlDto[] = [];
    selectedIl: IlDto = this.getEmptyIl();
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
    expandedRowKeys: Record<string, boolean> = {};
    tesislerByIlId: Record<number, TesisDto[]> = {};
    tesisLoadingByIlId: Record<number, boolean> = {};

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('IlYonetimi.Manage');
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
        this.loadIller(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadIller(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadIller(this.pageNumber, this.pageSize);
    }

    onRowExpand(event: { data?: IlDto }): void {
        const ilId = event.data?.id;
        if (!ilId || ilId <= 0) {
            return;
        }

        if (this.tesislerByIlId[ilId] || this.tesisLoadingByIlId[ilId]) {
            return;
        }

        this.tesisLoadingByIlId[ilId] = true;
        this.service
            .getTesislerByIl(ilId)
            .pipe(
                finalize(() => {
                    this.tesisLoadingByIlId[ilId] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (tesisler) => {
                    this.tesislerByIlId[ilId] = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getTesisler(ilId: number | null | undefined): TesisDto[] {
        if (!ilId || ilId <= 0) {
            return [];
        }

        return this.tesislerByIlId[ilId] ?? [];
    }

    isTesisLoading(ilId: number | null | undefined): boolean {
        if (!ilId || ilId <= 0) {
            return false;
        }

        return this.tesisLoadingByIlId[ilId] ?? false;
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedIl = this.getEmptyIl();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(il: IlDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedIl = { ...il };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(il: IlDto): void {
        this.selectedIl = { ...il };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: IlDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedIl.id ? this.service.updateIl(this.selectedIl.id, payload) : this.service.createIl(payload);

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
                    this.loadIller(this.pageNumber, this.pageSize);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.dialogMode === 'edit' ? 'Il guncellendi.' : 'Il olusturuldu.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteIl(il: IlDto): void {
        if (!this.canManage || !il.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${il.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteIl(il.id!).subscribe({
                    next: () => {
                        this.loadIller(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Il silindi.' });
                        this.cdr.detectChanges();
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                        this.cdr.detectChanges();
                    }
                });
            }
        });
    }

    private loadIller(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getIllerPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
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
                        this.loadIller(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.iller = pagedResponse.items;
                    this.pageNumber = pagedResponse.pageNumber;
                    this.pageSize = pagedResponse.pageSize;
                    this.totalRecords = pagedResponse.totalCount;
                    this.expandedRowKeys = {};
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
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

    private getEmptyIl(): IlDto {
        return {
            ad: '',
            aktifMi: true
        };
    }
}


import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { SezonDialog } from './sezon-dialog';
import { SezonKuraliDto } from './sezon-yonetimi.dto';
import { SezonYonetimiService } from './sezon-yonetimi.service';

interface SelectOption {
    label: string;
    value: number;
}

@Component({
    selector: 'app-sezon-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, SezonDialog],
    templateUrl: './sezon-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class SezonYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(SezonYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    sezonKurallari: SezonKuraliDto[] = [];
    tesisOptions: SelectOption[] = [];
    selectedTesisId: number | null = null;
    selectedModel: SezonKuraliDto = this.getEmptyModel();
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    sortBy = 'baslangicTarihi';
    sortDir: SortDirection = 'desc';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('SezonYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadInitialData();
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
        this.loadPaged();
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadPaged();
            this.searchDebounceHandle = null;
        }, 300);
    }

    onTesisFilterChanged(): void {
        this.pageNumber = 1;
        this.loadPaged();
    }

    refresh(): void {
        this.loadPaged();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedModel = this.getEmptyModel();
        this.selectedModel.tesisId = this.selectedTesisId ?? this.tesisOptions[0]?.value ?? 0;
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: SezonKuraliDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedModel = { ...item };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(item: SezonKuraliDto): void {
        this.selectedModel = { ...item };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: SezonKuraliDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedModel.id
                ? this.service.update(this.selectedModel.id, payload)
                : this.service.create(payload);

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
                    this.loadPaged();
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Sezon kurali guncellendi.' : 'Sezon kurali olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    delete(item: SezonKuraliDto): void {
        if (!this.canManage || !item.id) {
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
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.loadPaged();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Sezon kurali silindi.' });
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

    getTesisLabel(tesisId: number): string {
        return this.tesisOptions.find((x) => x.value === tesisId)?.label ?? `#${tesisId}`;
    }

    private loadInitialData(): void {
        this.loading = true;
        forkJoin({
            tesisler: this.service.getTesisler()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler }) => {
                    this.tesisOptions = [...tesisler]
                        .sort((a, b) => a.ad.localeCompare(b.ad))
                        .map((x) => ({ label: x.ad, value: x.id! }));
                    this.loadPaged();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadPaged(): void {
        this.loading = true;
        this.service
            .getPaged(this.pageNumber, this.pageSize, this.searchQuery, this.sortBy, this.sortDir, this.selectedTesisId)
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && this.pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadPaged();
                        return;
                    }

                    this.sezonKurallari = pagedResponse.items;
                    this.pageNumber = pagedResponse.pageNumber;
                    this.pageSize = pagedResponse.pageSize;
                    this.totalRecords = pagedResponse.totalCount;
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

    private getEmptyModel(): SezonKuraliDto {
        return {
            tesisId: 0,
            kod: '',
            ad: '',
            baslangicTarihi: '',
            bitisTarihi: '',
            minimumGece: 1,
            stopSaleMi: false,
            aktifMi: true
        };
    }
}

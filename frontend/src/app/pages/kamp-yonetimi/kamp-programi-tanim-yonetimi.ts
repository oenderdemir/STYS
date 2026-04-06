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
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KampProgramiDialog } from './kamp-programi-dialog';
import { KampProgramiDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-programi-tanim-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, KampProgramiDialog],
    templateUrl: './kamp-programi-tanim-yonetimi.html',
    styleUrl: './kamp-programi-tanim-yonetimi.scss',
    providers: [MessageService, ConfirmationService]
})
export class KampProgramiTanimYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    kampProgramlari: KampProgramiDto[] = [];
    selectedKampProgrami: KampProgramiDto = this.getEmptyModel();

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
        return this.hasAnyPermission('KampProgramiTanimYonetimi.Manage', 'KampProgramiYonetimi.Manage');
    }

    get canViewDonemler(): boolean {
        return this.hasAnyPermission('KampDonemiTanimYonetimi.View', 'KampDonemiYonetimi.View');
    }

    get canViewPuanKurallari(): boolean {
        return this.hasAnyPermission('KampPuanKuraliYonetimi.View', 'KampPuanKuraliYonetimi.Menu');
    }

    ngOnInit(): void {
        this.loadKampProgramlari(this.pageNumber, this.pageSize);
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
        this.loadKampProgramlari(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadKampProgramlari(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadKampProgramlari(this.pageNumber, this.pageSize);
    }

    openDonemler(): void {
        void this.router.navigate(['/kamp-donemleri']);
    }

    openPuanKurallari(): void {
        void this.router.navigate(['/kamp-puan-kurallari']);
    }

    openNew(): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.selectedKampProgrami = this.getEmptyModel();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: KampProgramiDto): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.openDialogWithDetail(item.id ?? null, 'edit');
    }

    openView(item: KampProgramiDto): void {
        this.openDialogWithDetail(item.id ?? null, 'view');
    }

    onDialogSave(payload: KampProgramiDto): void {
        if (this.saving || !this.canManageGlobal) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedKampProgrami.id
                ? this.service.updateKampProgrami(this.selectedKampProgrami.id, payload)
                : this.service.createKampProgrami(payload);

        this.saving = true;
        save$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadKampProgramlari(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Kamp programi guncellendi.' : 'Kamp programi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteKampProgrami(item: KampProgramiDto): void {
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
                this.service.deleteKampProgrami(item.id!).subscribe({
                    next: () => {
                        this.loadKampProgramlari(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kamp programi silindi.' });
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

    private loadKampProgramlari(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getKampProgramlariPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadKampProgramlari(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.kampProgramlari = pagedResponse.items;
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
            .getKampProgramiById(id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (item) => {
                    this.selectedKampProgrami = item;
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

    private getEmptyModel(): KampProgramiDto {
        return {
            kod: '',
            ad: '',
            aciklama: null,
            aktifMi: true
        };
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { Router } from '@angular/router';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.dto';
import { IsletmeAlaniDialog } from './isletme-alani-dialog';
import { IsletmeAlaniDto } from './isletme-alani-yonetimi.dto';
import { IsletmeAlaniSinifiDto } from './isletme-alani-yonetimi.dto';
import { IsletmeAlaniYonetimiService } from './isletme-alani-yonetimi.service';

@Component({
    selector: 'app-isletme-alani-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, IsletmeAlaniDialog],
    templateUrl: './isletme-alani-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IsletmeAlaniYonetimi implements OnDestroy {
    private readonly service = inject(IsletmeAlaniYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly router = inject(Router);

    alanlar: IsletmeAlaniDto[] = [];
    binalar: BinaDto[] = [];
    siniflar: IsletmeAlaniSinifiDto[] = [];
    selectedAlan: IsletmeAlaniDto = this.getEmptyAlan();
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

    get canView(): boolean {
        return this.canManage || this.authService.hasPermission('IsletmeAlaniYonetimi.View');
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
        this.loadData(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadData(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadData(this.pageNumber, this.pageSize);
    }

    openSinifYonetimi(): void {
        if (!this.canView) {
            return;
        }

        void this.router.navigate(['/isletme-alani-siniflari']);
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedAlan = this.getEmptyAlan();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(alan: IsletmeAlaniDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedAlan = { ...alan };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(alan: IsletmeAlaniDto): void {
        this.selectedAlan = { ...alan };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: IsletmeAlaniDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedAlan.id ? this.service.updateAlan(this.selectedAlan.id, payload) : this.service.createAlan(payload);

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
                    this.loadData(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Isletme alani guncellendi.' : 'Isletme alani olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteAlan(alan: IsletmeAlaniDto): void {
        if (!this.canManage || !alan.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${alan.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteAlan(alan.id!).subscribe({
                    next: () => {
                        this.loadData(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Isletme alani silindi.' });
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

    getBinaAdi(binaId: number): string {
        const bina = this.binalar.find((x) => x.id === binaId);
        return bina?.ad ?? '-';
    }

    getSinifAdi(alan: IsletmeAlaniDto): string {
        if (alan.isletmeAlaniSinifiAd && alan.isletmeAlaniSinifiAd.trim().length > 0) {
            return alan.isletmeAlaniSinifiAd;
        }

        const sinif = this.siniflar.find((x) => x.id === alan.isletmeAlaniSinifiId);
        return sinif?.ad ?? '-';
    }

    private loadData(pageNumber: number, pageSize: number): void {
        this.loading = true;
        forkJoin({
            alanlar: this.service.getAlanlarPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir),
            binalar: this.service.getBinalar(),
            siniflar: this.service.getSiniflar()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ alanlar, binalar, siniflar }) => {
                    if (alanlar.totalCount > 0 && alanlar.totalPages > 0 && pageNumber > alanlar.totalPages) {
                        this.pageNumber = alanlar.totalPages;
                        this.loadData(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.alanlar = alanlar.items;
                    this.pageNumber = alanlar.pageNumber;
                    this.pageSize = alanlar.pageSize;
                    this.totalRecords = alanlar.totalCount;
                    this.binalar = [...binalar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.siniflar = [...siniflar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyAlan(): IsletmeAlaniDto {
        return {
            ad: '',
            binaId: 0,
            isletmeAlaniSinifiId: 0,
            isletmeAlaniSinifiAd: null,
            ozelAd: null,
            aktifMi: true
        };
    }
}

import { UiSeverity } from '@/app/core/ui/ui-severity.constants';

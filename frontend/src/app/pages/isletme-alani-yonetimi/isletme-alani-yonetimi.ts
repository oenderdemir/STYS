import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.service';
import { IsletmeAlaniDto, IsletmeAlaniYonetimiService } from './isletme-alani-yonetimi.service';

@Component({
    selector: 'app-isletme-alani-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './isletme-alani-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IsletmeAlaniYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(IsletmeAlaniYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    alanlar: IsletmeAlaniDto[] = [];
    binalar: BinaDto[] = [];
    selectedAlan: IsletmeAlaniDto = this.getEmptyAlan();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('IsletmeAlaniYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadData(this.pageNumber, this.pageSize);
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
        this.pageNumber = nextPageNumber;
        this.pageSize = nextPageSize;
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

    openNew(): void {
        this.selectedAlan = this.getEmptyAlan();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(alan: IsletmeAlaniDto): void {
        this.selectedAlan = { ...alan };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveAlan(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: IsletmeAlaniDto = {
            ad: this.selectedAlan.ad.trim(),
            binaId: this.selectedAlan.binaId,
            aktifMi: this.selectedAlan.aktifMi
        };

        if (!payload.ad || !payload.binaId) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Alan adi ve bina secimi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedAlan.id ? this.service.updateAlan(this.selectedAlan.id, payload) : this.service.createAlan(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Isletme alani guncellendi.' : 'Isletme alani olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Isletme alani silindi.' });
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

    getBinaAdi(binaId: number): string {
        const bina = this.binalar.find((x) => x.id === binaId);
        return bina?.ad ?? '-';
    }

    private loadData(pageNumber: number, pageSize: number): void {
        this.loading = true;
        forkJoin({
            alanlar: this.service.getAlanlarPaged(pageNumber, pageSize, this.searchQuery),
            binalar: this.service.getBinalar()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ alanlar, binalar }) => {
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

    private getEmptyAlan(): IsletmeAlaniDto {
        return {
            ad: '',
            binaId: 0,
            aktifMi: true
        };
    }
}

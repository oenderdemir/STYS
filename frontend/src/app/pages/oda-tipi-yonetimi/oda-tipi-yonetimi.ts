import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { OdaTipiDto, OdaTipiYonetimiService } from './oda-tipi-yonetimi.service';

@Component({
    selector: 'app-oda-tipi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './oda-tipi-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class OdaTipiYonetimi implements OnDestroy {
    private readonly service = inject(OdaTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    odaTipleri: OdaTipiDto[] = [];
    selectedOdaTipi: OdaTipiDto = this.getEmptyOdaTipi();
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
        return this.authService.hasPermission('OdaTipiYonetimi.Manage');
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
        this.loadOdaTipleri(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadOdaTipleri(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadOdaTipleri(this.pageNumber, this.pageSize);
    }

    openNew(): void {
        this.selectedOdaTipi = this.getEmptyOdaTipi();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(odaTipi: OdaTipiDto): void {
        this.selectedOdaTipi = { ...odaTipi };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveOdaTipi(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: OdaTipiDto = {
            ad: this.selectedOdaTipi.ad.trim(),
            paylasimliMi: this.selectedOdaTipi.paylasimliMi,
            kapasite: this.selectedOdaTipi.kapasite,
            balkonVarMi: this.selectedOdaTipi.balkonVarMi,
            klimaVarMi: this.selectedOdaTipi.klimaVarMi,
            metrekare: this.selectedOdaTipi.metrekare ?? null,
            aktifMi: this.selectedOdaTipi.aktifMi
        };

        if (!payload.ad || payload.kapasite <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ad ve pozitif kapasite zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedOdaTipi.id ? this.service.updateOdaTipi(this.selectedOdaTipi.id, payload) : this.service.createOdaTipi(payload);

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
                    this.loadOdaTipleri(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Oda tipi guncellendi.' : 'Oda tipi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteOdaTipi(odaTipi: OdaTipiDto): void {
        if (!this.canManage || !odaTipi.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${odaTipi.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteOdaTipi(odaTipi.id!).subscribe({
                    next: () => {
                        this.loadOdaTipleri(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda tipi silindi.' });
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

    private loadOdaTipleri(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getOdaTipleriPaged(pageNumber, pageSize, this.searchQuery)
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
                        this.loadOdaTipleri(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.odaTipleri = pagedResponse.items;
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

    private getEmptyOdaTipi(): OdaTipiDto {
        return {
            ad: '',
            paylasimliMi: false,
            kapasite: 1,
            balkonVarMi: false,
            klimaVarMi: false,
            metrekare: null,
            aktifMi: true
        };
    }
}

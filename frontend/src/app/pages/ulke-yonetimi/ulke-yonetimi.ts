import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { UlkeDto, UlkeYonetimiService } from './ulke-yonetimi.service';

@Component({
    selector: 'app-ulke-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './ulke-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class UlkeYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(UlkeYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    ulkeler: UlkeDto[] = [];
    selectedUlke: UlkeDto = this.getEmptyUlke();
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
        return this.authService.hasPermission('CountryManagement.Manage');
    }

    ngOnInit(): void {
        this.loadUlkeler(this.pageNumber, this.pageSize);
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
        this.loadUlkeler(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadUlkeler(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadUlkeler(this.pageNumber, this.pageSize);
    }

    openNew(): void {
        this.selectedUlke = this.getEmptyUlke();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(ulke: UlkeDto): void {
        this.selectedUlke = { ...ulke };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveUlke(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: UlkeDto = {
            name: this.selectedUlke.name.trim(),
            code: this.selectedUlke.code.trim().toUpperCase()
        };

        if (!payload.name) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ulke adi zorunludur.' });
            return;
        }

        if (!payload.code) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ulke kodu zorunludur.' });
            return;
        }

        const save$: Observable<unknown> =
            this.isEditMode && this.selectedUlke.id
                ? this.service.updateUlke(this.selectedUlke.id, payload)
                : this.service.createUlke(payload);

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
                    this.loadUlkeler(this.pageNumber, this.pageSize);
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Ulke guncellendi.' : 'Ulke olusturuldu.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteUlke(ulke: UlkeDto): void {
        if (!this.canManage || !ulke.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${ulke.name}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteUlke(ulke.id!).subscribe({
                    next: () => {
                        this.loadUlkeler(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Ulke silindi.' });
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

    private loadUlkeler(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getUlkelerPaged(pageNumber, pageSize, this.searchQuery)
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
                        this.loadUlkeler(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.ulkeler = pagedResponse.items;
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

    private getEmptyUlke(): UlkeDto {
        return {
            name: '',
            code: ''
        };
    }
}

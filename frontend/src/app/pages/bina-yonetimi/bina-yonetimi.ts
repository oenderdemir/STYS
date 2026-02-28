import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { IsletmeAlaniDto } from '../isletme-alani-yonetimi/isletme-alani-yonetimi.dto';
import { OdaDto } from '../oda-yonetimi/oda-yonetimi.dto';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { BinaDto } from './bina-yonetimi.dto';
import { BinaYonetimiService } from './bina-yonetimi.service';

@Component({
    selector: 'app-bina-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './bina-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class BinaYonetimi implements OnDestroy {
    private readonly service = inject(BinaYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    binalar: BinaDto[] = [];
    tesisler: TesisDto[] = [];
    selectedBina: BinaDto = this.getEmptyBina();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    expandedRowKeys: Record<string, boolean> = {};
    odalarByBinaId: Record<number, OdaDto[]> = {};
    alanlarByBinaId: Record<number, IsletmeAlaniDto[]> = {};
    detailLoadingByBinaId: Record<number, boolean> = {};

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('BinaYonetimi.Manage');
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

    onRowExpand(event: { data?: BinaDto }): void {
        const binaId = event.data?.id;
        if (!binaId || binaId <= 0) {
            return;
        }

        if ((this.odalarByBinaId[binaId] && this.alanlarByBinaId[binaId]) || this.detailLoadingByBinaId[binaId]) {
            return;
        }

        this.detailLoadingByBinaId[binaId] = true;
        forkJoin({
            odalar: this.service.getOdalarByBina(binaId),
            alanlar: this.service.getAlanlarByBina(binaId)
        })
            .pipe(
                finalize(() => {
                    this.detailLoadingByBinaId[binaId] = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ odalar, alanlar }) => {
                    this.odalarByBinaId[binaId] = [...odalar].sort((left, right) => (left.odaNo ?? '').localeCompare(right.odaNo ?? ''));
                    this.alanlarByBinaId[binaId] = [...alanlar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    getOdalar(binaId: number | null | undefined): OdaDto[] {
        if (!binaId || binaId <= 0) {
            return [];
        }

        return this.odalarByBinaId[binaId] ?? [];
    }

    getAlanlar(binaId: number | null | undefined): IsletmeAlaniDto[] {
        if (!binaId || binaId <= 0) {
            return [];
        }

        return this.alanlarByBinaId[binaId] ?? [];
    }

    isDetailLoading(binaId: number | null | undefined): boolean {
        if (!binaId || binaId <= 0) {
            return false;
        }

        return this.detailLoadingByBinaId[binaId] ?? false;
    }

    openNew(): void {
        this.selectedBina = this.getEmptyBina();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(bina: BinaDto): void {
        this.selectedBina = { ...bina };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveBina(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: BinaDto = {
            ad: this.selectedBina.ad.trim(),
            tesisId: this.selectedBina.tesisId,
            katSayisi: this.selectedBina.katSayisi,
            aktifMi: this.selectedBina.aktifMi
        };

        if (!payload.ad || !payload.tesisId || payload.katSayisi <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ad, tesis ve pozitif kat sayisi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedBina.id ? this.service.updateBina(this.selectedBina.id, payload) : this.service.createBina(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Bina guncellendi.' : 'Bina olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteBina(bina: BinaDto): void {
        if (!this.canManage || !bina.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${bina.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteBina(bina.id!).subscribe({
                    next: () => {
                        this.loadData(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Bina silindi.' });
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

    getTesisAdi(tesisId: number): string {
        const tesis = this.tesisler.find((x) => x.id === tesisId);
        return tesis?.ad ?? '-';
    }

    private loadData(pageNumber: number, pageSize: number): void {
        this.loading = true;
        forkJoin({
            binalar: this.service.getBinalarPaged(pageNumber, pageSize, this.searchQuery),
            tesisler: this.service.getTesisler()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ binalar, tesisler }) => {
                    if (binalar.totalCount > 0 && binalar.totalPages > 0 && pageNumber > binalar.totalPages) {
                        this.pageNumber = binalar.totalPages;
                        this.loadData(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.binalar = binalar.items;
                    this.pageNumber = binalar.pageNumber;
                    this.pageSize = binalar.pageSize;
                    this.totalRecords = binalar.totalCount;
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.expandedRowKeys = {};
                    this.odalarByBinaId = {};
                    this.alanlarByBinaId = {};
                    this.detailLoadingByBinaId = {};
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

    private getEmptyBina(): BinaDto {
        return {
            ad: '',
            tesisId: 0,
            katSayisi: 1,
            aktifMi: true
        };
    }
}

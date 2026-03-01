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
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { IsletmeAlaniDto } from '../isletme-alani-yonetimi/isletme-alani-yonetimi.dto';
import { OdaDto } from '../oda-yonetimi/oda-yonetimi.dto';
import { OdaDialog } from '../oda-yonetimi/oda-dialog';
import { OdaYonetimiService } from '../oda-yonetimi/oda-yonetimi.service';
import { OdaOzellikDto } from '../oda-ozellik-yonetimi/oda-ozellik-yonetimi.dto';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.dto';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { BinaDialog } from './bina-dialog';
import { BinaDto } from './bina-yonetimi.dto';
import { BinaYonetimiService } from './bina-yonetimi.service';

@Component({
    selector: 'app-bina-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, BinaDialog, OdaDialog],
    templateUrl: './bina-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class BinaYonetimi implements OnDestroy {
    private readonly service = inject(BinaYonetimiService);
    private readonly odaService = inject(OdaYonetimiService);
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
    dialogMode: CrudDialogMode = 'create';
    odaViewMode: CrudDialogMode = 'view';
    odaViewSaving = false;
    odaViewDialogVisible = false;
    selectedOdaForView: OdaDto = this.getEmptyOdaForView();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    expandedRowKeys: Record<string, boolean> = {};
    odalarByBinaId: Record<number, OdaDto[]> = {};
    alanlarByBinaId: Record<number, IsletmeAlaniDto[]> = {};
    detailLoadingByBinaId: Record<number, boolean> = {};
    odaTipleri: OdaTipiDto[] = [];
    odaOzellikleri: OdaOzellikDto[] = [];

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('BinaYonetimi.Manage');
    }

    get canManageOda(): boolean {
        return this.authService.hasPermission('OdaYonetimi.Manage');
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
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(bina: BinaDto): void {
        this.selectedBina = { ...bina };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(bina: BinaDto): void {
        this.selectedBina = { ...bina };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    openOdaView(oda: OdaDto): void {
        this.selectedOdaForView = {
            ...oda,
            odaOzellikDegerleri: (oda.odaOzellikDegerleri ?? []).map((item) => ({ ...item }))
        };
        this.odaViewMode = 'view';
        this.odaViewDialogVisible = true;
    }

    onOdaDialogSave(payload: OdaDto): void {
        if (this.odaViewSaving || this.odaViewMode !== 'edit' || !this.selectedOdaForView.id) {
            return;
        }

        this.odaViewSaving = true;
        this.odaService
            .updateOda(this.selectedOdaForView.id, payload)
            .pipe(
                finalize(() => {
                    this.odaViewSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.odaViewDialogVisible = false;
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda guncellendi.' });
                    this.loadData(this.pageNumber, this.pageSize);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    onDialogSave(payload: BinaDto): void {
        if (this.saving) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedBina.id ? this.service.updateBina(this.selectedBina.id, payload) : this.service.createBina(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Bina guncellendi.' : 'Bina olusturuldu.' });
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
            tesisler: this.service.getTesisler(),
            odaTipleri: this.service.getOdaTipleri(),
            odaOzellikleri: this.odaService.getOdaOzellikleriActive()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ binalar, tesisler, odaTipleri, odaOzellikleri }) => {
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
                    this.odaTipleri = [...odaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.odaOzellikleri = [...odaOzellikleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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
            aktifMi: true,
            yoneticiUserIds: null
        };
    }

    private getEmptyOdaForView(): OdaDto {
        return {
            odaNo: '',
            binaId: 0,
            tesisOdaTipiId: 0,
            katNo: 0,
            yatakSayisi: null,
            odaOzellikDegerleri: [],
            aktifMi: true
        };
    }
}

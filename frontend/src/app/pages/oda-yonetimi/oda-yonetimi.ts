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
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.dto';
import { OdaOzellikDto } from '../oda-ozellik-yonetimi/oda-ozellik-yonetimi.dto';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.dto';
import { OdaDialog } from './oda-dialog';
import { OdaDto } from './oda-yonetimi.dto';
import { OdaYonetimiService } from './oda-yonetimi.service';

@Component({
    selector: 'app-oda-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, OdaDialog],
    templateUrl: './oda-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class OdaYonetimi implements OnDestroy {
    private readonly service = inject(OdaYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    odalar: OdaDto[] = [];
    binalar: BinaDto[] = [];
    odaTipleri: OdaTipiDto[] = [];
    odaOzellikleri: OdaOzellikDto[] = [];
    selectedOda: OdaDto = this.getEmptyOda();
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canCreate(): boolean {
        return this.authService.hasPermission('OdaYonetimi.Manage');
    }

    get canEdit(): boolean {
        return this.authService.hasPermission('OdaYonetimi.Manage');
    }

    get canDelete(): boolean {
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

    openNew(): void {
        if (!this.canCreate) {
            return;
        }

        this.selectedOda = this.getEmptyOda();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(oda: OdaDto): void {
        if (!this.canEdit) {
            return;
        }

        this.selectedOda = this.cloneOda(oda);
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(oda: OdaDto): void {
        this.selectedOda = this.cloneOda(oda);
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: OdaDto): void {
        if (this.saving) {
            return;
        }

        if (this.dialogMode === 'create' && !this.canCreate) {
            return;
        }

        if (this.dialogMode === 'edit' && !this.canEdit) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedOda.id ? this.service.updateOda(this.selectedOda.id, payload) : this.service.createOda(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Oda guncellendi.' : 'Oda olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteOda(oda: OdaDto): void {
        if (!this.canDelete || !oda.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${oda.odaNo}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteOda(oda.id!).subscribe({
                    next: () => {
                        this.loadData(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda silindi.' });
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

    getOdaTipiAdi(tesisOdaTipiId: number): string {
        const odaTipi = this.odaTipleri.find((x) => x.id === tesisOdaTipiId);
        return odaTipi?.ad ?? '-';
    }

    getDinamikOzelliklerText(oda: OdaDto): string {
        const values = oda.odaOzellikDegerleri ?? [];
        if (values.length === 0) {
            return '-';
        }

        const text = values
            .map((value) => {
                const feature = this.odaOzellikleri.find((item) => item.id === value.odaOzellikId);
                const featureName = feature?.ad ?? `#${value.odaOzellikId}`;
                return `${featureName}: ${this.formatFeatureValue(feature, value.deger ?? null)}`;
            })
            .join(', ');

        return text.length > 0 ? text : '-';
    }

    private loadData(pageNumber: number, pageSize: number): void {
        this.loading = true;
        forkJoin({
            odalar: this.service.getOdalarPaged(pageNumber, pageSize, this.searchQuery),
            binalar: this.service.getBinalar(),
            odaTipleri: this.service.getOdaTipleri(),
            odaOzellikleri: this.service.getOdaOzellikleriActive()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ odalar, binalar, odaTipleri, odaOzellikleri }) => {
                    if (odalar.totalCount > 0 && odalar.totalPages > 0 && pageNumber > odalar.totalPages) {
                        this.pageNumber = odalar.totalPages;
                        this.loadData(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.odalar = odalar.items;
                    this.pageNumber = odalar.pageNumber;
                    this.pageSize = odalar.pageSize;
                    this.totalRecords = odalar.totalCount;
                    this.binalar = [...binalar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.odaTipleri = [...odaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.odaOzellikleri = [...odaOzellikleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyOda(): OdaDto {
        return {
            odaNo: '',
            binaId: 0,
            tesisOdaTipiId: 0,
            katNo: 0,
            odaOzellikDegerleri: [],
            aktifMi: true
        };
    }

    private cloneOda(source: OdaDto): OdaDto {
        return {
            ...source,
            odaOzellikDegerleri: (source.odaOzellikDegerleri ?? []).map((item) => ({ ...item }))
        };
    }

    private formatFeatureValue(feature: OdaOzellikDto | undefined, value: string | null): string {
        const normalizedValue = value?.trim() ?? '';
        if (normalizedValue.length === 0) {
            return '-';
        }

        if (feature?.veriTipi === 'boolean') {
            if (normalizedValue === 'true') {
                return 'Evet';
            }

            if (normalizedValue === 'false') {
                return 'Hayir';
            }
        }

        return normalizedValue;
    }
}

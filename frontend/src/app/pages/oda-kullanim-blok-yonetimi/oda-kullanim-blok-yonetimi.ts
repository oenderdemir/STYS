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
import { OdaKullanimBlokDialog } from './oda-kullanim-blok-dialog';
import { OdaKullanimBlokDto, OdaKullanimBlokOdaSecenekDto, OdaKullanimBlokTesisDto } from './oda-kullanim-blok-yonetimi.dto';
import { OdaKullanimBlokYonetimiService } from './oda-kullanim-blok-yonetimi.service';

interface SelectOption<T = string | number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-oda-kullanim-blok-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, OdaKullanimBlokDialog],
    templateUrl: './oda-kullanim-blok-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class OdaKullanimBlokYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(OdaKullanimBlokYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    kayitlar: OdaKullanimBlokDto[] = [];
    tesisler: OdaKullanimBlokTesisDto[] = [];
    odaSecenekleri: OdaKullanimBlokOdaSecenekDto[] = [];
    tesisOptions: SelectOption<number>[] = [];
    odaOptions: SelectOption<number>[] = [];
    blokTipiOptions: SelectOption<string>[] = [
        { label: 'Bakim', value: 'Bakim' },
        { label: 'Ariza', value: 'Ariza' }
    ];

    selectedTesisId: number | null = null;
    selectedOdaId: number | null = null;
    selectedBlokTipi: string | null = null;
    selectedModel: OdaKullanimBlokDto = this.getEmptyModel();

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

    get canView(): boolean {
        return this.authService.hasPermission('OdaKullanimBlokYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('OdaKullanimBlokYonetimi.Manage');
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
        this.selectedOdaId = null;
        this.pageNumber = 1;
        this.loadOdalarForSelectedTesis(true);
    }

    onOdaFilterChanged(): void {
        this.pageNumber = 1;
        this.loadPaged();
    }

    onBlokTipiFilterChanged(): void {
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

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen once tesis seciniz.' });
            return;
        }

        this.selectedModel = this.getEmptyModel();
        this.selectedModel.tesisId = this.selectedTesisId;
        this.selectedModel.baslangicTarihi = this.nowInput();
        this.selectedModel.bitisTarihi = this.nextHourInput();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: OdaKullanimBlokDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedTesisId = item.tesisId;
        this.selectedOdaId = null;
        this.loadOdalarForSelectedTesis();
        this.selectedModel = { ...item };
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(item: OdaKullanimBlokDto): void {
        this.selectedTesisId = item.tesisId;
        this.selectedOdaId = null;
        this.loadOdalarForSelectedTesis();
        this.selectedModel = { ...item };
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: OdaKullanimBlokDto): void {
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
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Kayit guncellendi.' : 'Kayit olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    delete(item: OdaKullanimBlokDto): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `#${item.id} numarali kaydi silmek istediginize emin misiniz?`,
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
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
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

    getTesisLabel(tesisId: number): string {
        return this.tesisOptions.find((x) => x.value === tesisId)?.label ?? `#${tesisId}`;
    }

    getOdaLabel(odaId: number): string {
        return this.odaOptions.find((x) => x.value === odaId)?.label ?? `#${odaId}`;
    }

    private loadInitialData(): void {
        if (!this.canView) {
            return;
        }

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
                    this.tesisler = [...tesisler].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.tesisOptions = this.tesisler.map((x) => ({ label: x.ad, value: x.id }));

                    if (!this.selectedTesisId && this.tesisOptions.length > 0) {
                        this.selectedTesisId = this.tesisOptions[0].value;
                    }

                    this.loadOdalarForSelectedTesis(true);
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadOdalarForSelectedTesis(reloadPagedAfter = false): void {
        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.odaSecenekleri = [];
            this.odaOptions = [];
            this.selectedOdaId = null;
            if (reloadPagedAfter) {
                this.loadPaged();
            }
            return;
        }

        this.service.getOdalar(this.selectedTesisId).subscribe({
            next: (odalar) => {
                this.odaSecenekleri = [...odalar];
                this.odaOptions = odalar
                    .map((x) => ({
                        label: `${x.odaNo} - ${x.binaAdi} (${x.odaTipiAdi})`,
                        value: x.id
                    }))
                    .sort((a, b) => a.label.localeCompare(b.label));
                if (reloadPagedAfter) {
                    this.loadPaged();
                } else {
                    this.cdr.detectChanges();
                }
            },
            error: (error: unknown) => {
                this.odaSecenekleri = [];
                this.odaOptions = [];
                this.selectedOdaId = null;
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                this.cdr.detectChanges();
            }
        });
    }

    private loadPaged(): void {
        if (!this.canView) {
            return;
        }

        this.loading = true;
        this.service
            .getPaged(this.pageNumber, this.pageSize, this.searchQuery, this.sortBy, this.sortDir, this.selectedTesisId, this.selectedOdaId, this.selectedBlokTipi)
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

                    this.kayitlar = pagedResponse.items;
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

    private getEmptyModel(): OdaKullanimBlokDto {
        return {
            tesisId: this.selectedTesisId ?? 0,
            odaId: 0,
            blokTipi: 'Bakim',
            baslangicTarihi: this.nowInput(),
            bitisTarihi: this.nextHourInput(),
            aciklama: null,
            aktifMi: true
        };
    }

    private nowInput(): string {
        return this.toDateTimeLocalInput(new Date());
    }

    private nextHourInput(): string {
        const now = new Date();
        now.setHours(now.getHours() + 1);
        return this.toDateTimeLocalInput(now);
    }

    private toDateTimeLocalInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}:00`;
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
}

import { UiSeverity } from '@/app/core/ui/ui-severity.constants';

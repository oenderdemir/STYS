import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KonaklamaTipiDialog } from './konaklama-tipi-dialog';
import { KonaklamaTipiDto, KonaklamaTipiTesisAtamaDto } from './konaklama-tipi-yonetimi.dto';
import { KonaklamaTipiYonetimiService } from './konaklama-tipi-yonetimi.service';

@Component({
    selector: 'app-konaklama-tipi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule, KonaklamaTipiDialog],
    templateUrl: './konaklama-tipi-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class KonaklamaTipiYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(KonaklamaTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    konaklamaTipleri: KonaklamaTipiDto[] = [];
    selectedKonaklamaTipi: KonaklamaTipiDto = this.getEmptyModel();
    tesisAtamalari: KonaklamaTipiTesisAtamaDto[] = [];
    selectedTesisId: number | null = null;

    loading = false;
    loadingBaglam = false;
    loadingAtamalar = false;
    saving = false;
    savingAtamalar = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    sortBy = 'ad';
    sortDir: SortDirection = 'asc';
    globalTipYonetimiYapabilirMi = false;
    tesisSecenekleri: Array<{ label: string; value: number }> = [];

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('KonaklamaTipiYonetimi.Manage');
    }

    get canManageGlobal(): boolean {
        return this.canManage && this.globalTipYonetimiYapabilirMi;
    }

    get canManageAssignments(): boolean {
        return this.canManage && !!this.selectedTesisId;
    }

    ngOnInit(): void {
        this.loadPageContext();
    }

    ngOnDestroy(): void {
        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
            this.searchDebounceHandle = null;
        }
    }

    onLazyLoad(event: LazyLoadPayload): void {
        if (!this.canManageGlobal) {
            return;
        }

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
        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        if (!this.canManageGlobal) {
            return;
        }

        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadPageContext();
    }

    onTesisChange(): void {
        this.loadTesisAtamalari();
    }

    openNew(): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.selectedKonaklamaTipi = this.getEmptyModel();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: KonaklamaTipiDto): void {
        if (!this.canManageGlobal) {
            return;
        }
        this.openDialogWithDetail(item.id ?? null, 'edit');
    }

    openView(item: KonaklamaTipiDto): void {
        this.openDialogWithDetail(item.id ?? null, 'view');
    }

    onDialogSave(payload: KonaklamaTipiDto): void {
        if (this.saving || !this.canManageGlobal) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedKonaklamaTipi.id
                ? this.service.updateKonaklamaTipi(this.selectedKonaklamaTipi.id, payload)
                : this.service.createKonaklamaTipi(payload);

        this.saving = true;
        save$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                    this.loadTesisAtamalari();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Konaklama tipi guncellendi.' : 'Konaklama tipi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteKonaklamaTipi(item: KonaklamaTipiDto): void {
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
                this.service.deleteKonaklamaTipi(item.id!).subscribe({
                    next: () => {
                        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                        this.loadTesisAtamalari();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklama tipi silindi.' });
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

    toggleTesisAtamasi(item: KonaklamaTipiTesisAtamaDto, checked: boolean): void {
        item.tesisteKullanilabilirMi = checked;
    }

    kaydetTesisAtamalari(): void {
        if (!this.canManageAssignments || !this.selectedTesisId || this.savingAtamalar) {
            return;
        }

        const konaklamaTipiIds = this.tesisAtamalari
            .filter((x) => x.tesisteKullanilabilirMi && x.globalAktifMi)
            .map((x) => x.konaklamaTipiId);

        this.savingAtamalar = true;
        this.service
            .kaydetTesisAtamalari(this.selectedTesisId, konaklamaTipiIds)
            .pipe(finalize(() => {
                this.savingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Tesis konaklama tipi atamalari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByKonaklamaTipi(index: number, item: KonaklamaTipiTesisAtamaDto): number {
        return item.konaklamaTipiId;
    }

    private loadPageContext(): void {
        this.loadingBaglam = true;
        this.service
            .getYonetimBaglam()
            .pipe(finalize(() => {
                this.loadingBaglam = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.globalTipYonetimiYapabilirMi = baglam.globalTipYonetimiYapabilirMi;
                    this.tesisSecenekleri = baglam.tesisler
                        .map((x) => ({ label: x.ad, value: x.id }))
                        .sort((a, b) => a.label.localeCompare(b.label));

                    if (this.selectedTesisId && !this.tesisSecenekleri.some((x) => x.value === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId) {
                        this.selectedTesisId = this.tesisSecenekleri[0]?.value ?? null;
                    }

                    if (this.canManageGlobal) {
                        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                    } else {
                        this.konaklamaTipleri = [];
                        this.totalRecords = 0;
                    }

                    this.loadTesisAtamalari();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.globalTipYonetimiYapabilirMi = false;
                    this.tesisSecenekleri = [];
                    this.selectedTesisId = null;
                    this.konaklamaTipleri = [];
                    this.tesisAtamalari = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadKonaklamaTipleri(pageNumber: number, pageSize: number): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.loading = true;
        this.service
            .getKonaklamaTipleriPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadKonaklamaTipleri(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.konaklamaTipleri = pagedResponse.items;
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

    private loadTesisAtamalari(): void {
        if (!this.selectedTesisId) {
            this.tesisAtamalari = [];
            return;
        }

        this.loadingAtamalar = true;
        this.service
            .getTesisAtamalari(this.selectedTesisId)
            .pipe(finalize(() => {
                this.loadingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisAtamalari = [];
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

    private getEmptyModel(): KonaklamaTipiDto {
        return {
            kod: '',
            ad: '',
            aktifMi: true,
            icerikKalemleri: []
        };
    }

    private openDialogWithDetail(id: number | null, mode: CrudDialogMode): void {
        if (!id) {
            return;
        }

        this.saving = true;
        this.service
            .getKonaklamaTipiById(id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (item) => {
                    this.selectedKonaklamaTipi = item;
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
}

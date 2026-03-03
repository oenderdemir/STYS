import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, finalize, forkJoin, Observable, of } from 'rxjs';
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
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { IndirimKuraliDialog, SelectOption } from './indirim-kurali-dialog';
import { IndirimKuraliDto } from './indirim-kurali-yonetimi.dto';
import { IndirimKuraliYonetimiService } from './indirim-kurali-yonetimi.service';

@Component({
    selector: 'app-indirim-kurali-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, IndirimKuraliDialog],
    templateUrl: './indirim-kurali-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IndirimKuraliYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(IndirimKuraliYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    indirimKurallari: IndirimKuraliDto[] = [];
    selectedIndirimKurali: IndirimKuraliDto = this.getEmptyModel();
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';

    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';

    tesisOptions: SelectOption<number>[] = [];
    misafirTipiOptions: SelectOption<number>[] = [];
    konaklamaTipiOptions: SelectOption<number>[] = [];

    private readonly tesisNameMap = new Map<number, string>();
    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('OdaFiyatYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadLookups();
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
        this.loadIndirimKurallari(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadIndirimKurallari(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadLookups();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedIndirimKurali = this.getEmptyModel();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: IndirimKuraliDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedIndirimKurali = this.cloneModel(item);
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(item: IndirimKuraliDto): void {
        this.selectedIndirimKurali = this.cloneModel(item);
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: IndirimKuraliDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedIndirimKurali.id
                ? this.service.updateIndirimKurali(this.selectedIndirimKurali.id, payload)
                : this.service.createIndirimKurali(payload);

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
                    this.loadIndirimKurallari(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Indirim kurali guncellendi.' : 'Indirim kurali olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteIndirimKurali(item: IndirimKuraliDto): void {
        if (!this.canManage || !item.id) {
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
                this.service.deleteIndirimKurali(item.id!).subscribe({
                    next: () => {
                        this.loadIndirimKurallari(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Indirim kurali silindi.' });
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

    getTesisName(tesisId?: number | null): string {
        if (!tesisId) {
            return '-';
        }

        return this.tesisNameMap.get(tesisId) ?? `#${tesisId}`;
    }

    private loadLookups(): void {
        forkJoin({
            konaklama: this.service.getKonaklamaTipleri().pipe(catchError(() => of([]))),
            misafir: this.service.getMisafirTipleri().pipe(catchError(() => of([]))),
            tesis: this.service.getTesisler().pipe(catchError(() => of([])))
        }).subscribe({
            next: ({ konaklama, misafir, tesis }) => {
                this.konaklamaTipiOptions = konaklama
                    .filter((x) => !!x.id)
                    .map((x) => ({ label: x.ad, value: x.id! }))
                    .sort((a, b) => a.label.localeCompare(b.label));
                this.misafirTipiOptions = misafir
                    .filter((x) => !!x.id)
                    .map((x) => ({ label: x.ad, value: x.id! }))
                    .sort((a, b) => a.label.localeCompare(b.label));

                this.buildTesisLookups(tesis);
                this.loadIndirimKurallari(this.pageNumber, this.pageSize);
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                this.cdr.detectChanges();
            }
        });
    }

    private buildTesisLookups(tesisler: TesisDto[]): void {
        this.tesisNameMap.clear();
        this.tesisOptions = tesisler
            .filter((x) => !!x.id)
            .map((x) => ({ label: x.ad, value: x.id! }))
            .sort((a, b) => a.label.localeCompare(b.label));

        for (const option of this.tesisOptions) {
            this.tesisNameMap.set(option.value, option.label);
        }
    }

    private loadIndirimKurallari(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getIndirimKurallariPaged(pageNumber, pageSize, this.searchQuery)
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
                        this.loadIndirimKurallari(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.indirimKurallari = pagedResponse.items.map((item) => this.cloneModel(item));
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

    private cloneModel(source: IndirimKuraliDto): IndirimKuraliDto {
        return {
            id: source.id ?? null,
            kod: source.kod ?? '',
            ad: source.ad ?? '',
            indirimTipi: source.indirimTipi ?? 'Yuzde',
            deger: source.deger ?? 0,
            kapsamTipi: source.kapsamTipi ?? 'Sistem',
            tesisId: source.tesisId ?? null,
            baslangicTarihi: this.normalizeDateInput(source.baslangicTarihi),
            bitisTarihi: this.normalizeDateInput(source.bitisTarihi),
            oncelik: source.oncelik ?? 0,
            birlesebilirMi: source.birlesebilirMi ?? true,
            aktifMi: source.aktifMi ?? true,
            misafirTipiIds: [...(source.misafirTipiIds ?? [])],
            konaklamaTipiIds: [...(source.konaklamaTipiIds ?? [])]
        };
    }

    private normalizeDateInput(value: string): string {
        if (!value) {
            return this.todayInput();
        }

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return this.todayInput();
        }

        return parsed.toISOString().slice(0, 10);
    }

    private todayInput(): string {
        return new Date().toISOString().slice(0, 10);
    }

    private getEmptyModel(): IndirimKuraliDto {
        const today = this.todayInput();
        return {
            kod: '',
            ad: '',
            indirimTipi: 'Yuzde',
            deger: 0,
            kapsamTipi: 'Sistem',
            tesisId: null,
            baslangicTarihi: today,
            bitisTarihi: today,
            oncelik: 0,
            birlesebilirMi: true,
            aktifMi: true,
            misafirTipiIds: [],
            konaklamaTipiIds: []
        };
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

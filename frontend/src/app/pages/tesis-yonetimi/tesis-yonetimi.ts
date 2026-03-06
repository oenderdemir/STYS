import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, resolveSortFromLazyPayload, SortDirection, tryReadApiMessage } from '../../core/api';
import { ManagerCandidateDto } from '../../core/identity';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { AuthService } from '../auth';
import { IlDto } from '../il-yonetimi/il-yonetimi.dto';
import { TesisDialog } from './tesis-dialog';
import { TesisDto } from './tesis-yonetimi.dto';
import { TesisYonetimiService } from './tesis-yonetimi.service';

@Component({
    selector: 'app-tesis-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, TesisDialog],
    templateUrl: './tesis-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class TesisYonetimi implements OnDestroy {
    private readonly service = inject(TesisYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: TesisDto[] = [];
    iller: IlDto[] = [];
    yoneticiAdaylari: ManagerCandidateDto[] = [];
    resepsiyonistAdaylari: ManagerCandidateDto[] = [];
    selectedTesis: TesisDto = this.getEmptyTesis();
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
        return this.authService.hasPermission('TesisYonetimi.Manage');
    }

    get canAssignTesisYoneticisi(): boolean {
        return this.authService.hasPermission('KullaniciAtama.TesisYoneticisiAtayabilir');
    }

    get canAssignResepsiyonist(): boolean {
        return this.authService.hasPermission('KullaniciAtama.ResepsiyonistAtayabilir');
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

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedTesis = this.getEmptyTesis();
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(tesis: TesisDto): void {
        if (!this.canManage) {
            return;
        }

        this.selectedTesis = this.cloneTesis(tesis);
        this.dialogMode = 'edit';
        this.dialogVisible = true;
    }

    openView(tesis: TesisDto): void {
        this.selectedTesis = this.cloneTesis(tesis);
        this.dialogMode = 'view';
        this.dialogVisible = true;
    }

    onDialogSave(payload: TesisDto): void {
        if (this.saving || !this.canManage) {
            return;
        }

        const save$: Observable<unknown> = this.dialogMode === 'edit' && this.selectedTesis.id ? this.service.updateTesis(this.selectedTesis.id, payload) : this.service.createTesis(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Tesis guncellendi.' : 'Tesis olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteTesis(tesis: TesisDto): void {
        if (!this.canManage || !tesis.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${tesis.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteTesis(tesis.id!).subscribe({
                    next: () => {
                        this.loadData(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Tesis silindi.' });
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

    getIlAdi(ilId: number): string {
        const il = this.iller.find((x) => x.id === ilId);
        return il?.ad ?? '-';
    }

    private loadData(pageNumber: number, pageSize: number): void {
        this.loading = true;
        forkJoin({
            tesisler: this.service.getTesislerPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir),
            iller: this.service.getIller(),
            yoneticiAdaylari: this.canManage && this.canAssignTesisYoneticisi ? this.service.getYoneticiAdaylari() : of([]),
            resepsiyonistAdaylari: this.canManage && this.canAssignResepsiyonist ? this.service.getResepsiyonistAdaylari() : of([])
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, iller, yoneticiAdaylari, resepsiyonistAdaylari }) => {
                    if (tesisler.totalCount > 0 && tesisler.totalPages > 0 && pageNumber > tesisler.totalPages) {
                        this.pageNumber = tesisler.totalPages;
                        this.loadData(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.tesisler = tesisler.items;
                    this.pageNumber = tesisler.pageNumber;
                    this.pageSize = tesisler.pageSize;
                    this.totalRecords = tesisler.totalCount;
                    this.iller = [...iller].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.yoneticiAdaylari = [...yoneticiAdaylari].sort((left, right) => (left.userName ?? '').localeCompare(right.userName ?? ''));
                    this.resepsiyonistAdaylari = [...resepsiyonistAdaylari].sort((left, right) => (left.userName ?? '').localeCompare(right.userName ?? ''));
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

    private getEmptyTesis(): TesisDto {
        return {
            ad: '',
            ilId: 0,
            telefon: '',
            adres: '',
            eposta: null,
            girisSaati: '14:00',
            cikisSaati: '10:00',
            aktifMi: true,
            yoneticiUserIds: null,
            resepsiyonistUserIds: null
        };
    }

    private cloneTesis(source: TesisDto): TesisDto {
        return {
            ...source,
            girisSaati: source.girisSaati ?? '14:00',
            cikisSaati: source.cikisSaati ?? '10:00',
            yoneticiUserIds: [...(source.yoneticiUserIds ?? [])],
            resepsiyonistUserIds: [...(source.resepsiyonistUserIds ?? [])]
        };
    }
}


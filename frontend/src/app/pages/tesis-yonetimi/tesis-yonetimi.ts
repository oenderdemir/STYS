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
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { LazyLoadPayload, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { IlDto } from '../il-yonetimi/il-yonetimi.service';
import { TesisDto, TesisYonetimiService } from './tesis-yonetimi.service';

@Component({
    selector: 'app-tesis-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
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
    selectedTesis: TesisDto = this.getEmptyTesis();
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
        return this.authService.hasPermission('TesisYonetimi.Manage');
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
        this.selectedTesis = this.getEmptyTesis();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(tesis: TesisDto): void {
        this.selectedTesis = { ...tesis };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveTesis(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: TesisDto = {
            ad: this.selectedTesis.ad.trim(),
            ilId: this.selectedTesis.ilId,
            telefon: this.selectedTesis.telefon.trim(),
            adres: this.selectedTesis.adres.trim(),
            eposta: this.selectedTesis.eposta?.trim() || null,
            aktifMi: this.selectedTesis.aktifMi
        };

        if (!payload.ad || !payload.ilId || !payload.telefon || !payload.adres) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ad, il, telefon ve adres alanlari zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedTesis.id ? this.service.updateTesis(this.selectedTesis.id, payload) : this.service.createTesis(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Tesis guncellendi.' : 'Tesis olusturuldu.' });
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
            tesisler: this.service.getTesislerPaged(pageNumber, pageSize, this.searchQuery),
            iller: this.service.getIller()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, iller }) => {
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
            aktifMi: true
        };
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize, Observable } from 'rxjs';
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
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KampDonemiDialog } from './kamp-donemi-dialog';
import { KampDonemiDto, KampProgramiSecenekDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-donemi-tanim-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, KampDonemiDialog],
    templateUrl: './kamp-donemi-tanim-yonetimi.html',
    styleUrl: './kamp-donemi-tanim-yonetimi.scss',
    providers: [MessageService, ConfirmationService]
})
export class KampDonemiTanimYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    kampDonemleri: KampDonemiDto[] = [];
    selectedKampDonemi: KampDonemiDto = this.getEmptyModel();
    programlar: KampProgramiSecenekDto[] = [];

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: CrudDialogMode = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    searchQuery = '';
    sortBy = 'yil';
    sortDir: SortDirection = 'desc';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

    get canManageGlobal(): boolean {
        return this.hasAnyPermission('KampDonemiTanimYonetimi.Manage', 'KampDonemiYonetimi.Manage');
    }

    get canViewPrograms(): boolean {
        return this.hasAnyPermission('KampProgramiTanimYonetimi.View', 'KampProgramiYonetimi.View');
    }

    get canViewAssignments(): boolean {
        return this.hasAnyPermission('KampDonemiTesisAtamaYonetimi.View', 'KampDonemiYonetimi.View');
    }

    get canViewPuanKurallari(): boolean {
        return this.hasAnyPermission('KampPuanKuraliYonetimi.View', 'KampPuanKuraliYonetimi.Menu');
    }

    ngOnInit(): void {
        this.loadContextAndPage();
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
        this.loadKampDonemleri(this.pageNumber, this.pageSize);
    }

    onSearchInput(event: Event): void {
        const value = (event.target as HTMLInputElement).value;
        this.searchQuery = value;

        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
        }

        this.searchDebounceHandle = setTimeout(() => {
            this.pageNumber = 1;
            this.loadKampDonemleri(this.pageNumber, this.pageSize);
            this.searchDebounceHandle = null;
        }, 300);
    }

    refresh(): void {
        this.loadContextAndPage();
    }

    openPrograms(): void {
        void this.router.navigate(['/kamp-programlari']);
    }

    openAssignments(): void {
        void this.router.navigate(['/kamp-donemi-atamalari']);
    }

    openPuanKurallari(): void {
        void this.router.navigate(['/kamp-puan-kurallari']);
    }

    openNew(): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.selectedKampDonemi = this.getEmptyModel();
        this.selectedKampDonemi.kampProgramiId = this.programlar[0]?.id ?? 0;
        this.dialogMode = 'create';
        this.dialogVisible = true;
    }

    openEdit(item: KampDonemiDto): void {
        if (!this.canManageGlobal) {
            return;
        }

        this.openDialogWithDetail(item.id ?? null, 'edit');
    }

    openView(item: KampDonemiDto): void {
        this.openDialogWithDetail(item.id ?? null, 'view');
    }

    onDialogSave(payload: KampDonemiDto): void {
        if (this.saving || !this.canManageGlobal) {
            return;
        }

        const save$: Observable<unknown> =
            this.dialogMode === 'edit' && this.selectedKampDonemi.id
                ? this.service.updateKampDonemi(this.selectedKampDonemi.id, payload)
                : this.service.createKampDonemi(payload);

        this.saving = true;
        save$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadKampDonemleri(this.pageNumber, this.pageSize);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: this.dialogMode === 'edit' ? 'Kamp donemi guncellendi.' : 'Kamp donemi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteKampDonemi(item: KampDonemiDto): void {
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
                this.service.deleteKampDonemi(item.id!).subscribe({
                    next: () => {
                        this.loadKampDonemleri(this.pageNumber, this.pageSize);
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kamp donemi silindi.' });
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

    private loadContextAndPage(): void {
        this.loading = true;
        this.service.getKampDonemiYonetimBaglam()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.programlar = [...baglam.programlar].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.loadKampDonemleri(this.pageNumber, this.pageSize);
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.programlar = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadKampDonemleri(pageNumber: number, pageSize: number): void {
        this.loading = true;
        this.service
            .getKampDonemleriPaged(pageNumber, pageSize, this.searchQuery, this.sortBy, this.sortDir)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (pagedResponse) => {
                    if (pagedResponse.totalCount > 0 && pagedResponse.totalPages > 0 && pageNumber > pagedResponse.totalPages) {
                        this.pageNumber = pagedResponse.totalPages;
                        this.loadKampDonemleri(this.pageNumber, this.pageSize);
                        return;
                    }

                    this.kampDonemleri = pagedResponse.items;
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

    private openDialogWithDetail(id: number | null, mode: CrudDialogMode): void {
        if (!id) {
            return;
        }

        this.saving = true;
        this.service
            .getKampDonemiById(id)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (item) => {
                    this.selectedKampDonemi = item;
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

    private hasAnyPermission(...permissions: string[]): boolean {
        return permissions.some((permission) => this.authService.hasPermission(permission));
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

    private getEmptyModel(): KampDonemiDto {
        return {
            kampProgramiId: 0,
            kod: '',
            ad: '',
            yil: new Date().getFullYear(),
            basvuruBaslangicTarihi: '',
            basvuruBitisTarihi: '',
            konaklamaBaslangicTarihi: '',
            konaklamaBitisTarihi: '',
            minimumGece: 1,
            maksimumGece: 1,
            onayGerektirirMi: true,
            cekilisGerekliMi: false,
            ayniAileIcinTekBasvuruMu: true,
            iptalSonGun: null,
            aktifMi: true
        };
    }
}

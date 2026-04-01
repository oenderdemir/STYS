import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MenuItem, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { Menu, MenuModule } from 'primeng/menu';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { SortDirection, tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { OdaTemizlikKayitDto } from './oda-temizlik-yonetimi.dto';
import { OdaTemizlikYonetimiService } from './oda-temizlik-yonetimi.service';

interface SelectOption<T = string | number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-oda-temizlik-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, IconFieldModule, InputIconModule, InputTextModule, MenuModule, PaginatorModule, SelectModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './oda-temizlik-yonetimi.html',
    providers: [MessageService],
    styleUrl: './oda-temizlik-yonetimi.scss'
})
export class OdaTemizlikYonetimi implements OnInit, OnDestroy {
    private readonly service = inject(OdaTemizlikYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    @ViewChild('cardMenu') cardMenu?: Menu;

    kayitlar: OdaTemizlikKayitDto[] = [];
    tesisOptions: SelectOption<number>[] = [];
    durumOptions: SelectOption<string | null>[] = [
        { label: 'Bekleyenler (Kirli + Temizleniyor)', value: null },
        { label: 'Kirli', value: 'Kirli' },
        { label: 'Temizleniyor', value: 'Temizleniyor' },
        { label: 'Hazir', value: 'Hazir' }
    ];

    selectedTesisId: number | null = null;
    selectedDurum: string | null = null;
    selectedKayit: OdaTemizlikKayitDto | null = null;
    rowActionItems: MenuItem[] = [];

    loading = false;
    actionLoading = false;

    pageNumber = 1;
    pageSize = 24;
    totalRecords = 0;
    searchQuery = '';
    sortBy = 'odaNo';
    sortDir: SortDirection = 'asc';

    private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;
    private menuReopenHandle: ReturnType<typeof setTimeout> | null = null;
    private readonly roomImageUrl = 'demo/images/galleria/galleria2.jpg';

    get canView(): boolean {
        return this.authService.hasPermission('OdaTemizlikYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('OdaTemizlikYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadInitialData();
    }

    ngOnDestroy(): void {
        if (this.searchDebounceHandle !== null) {
            clearTimeout(this.searchDebounceHandle);
            this.searchDebounceHandle = null;
        }

        if (this.menuReopenHandle !== null) {
            clearTimeout(this.menuReopenHandle);
            this.menuReopenHandle = null;
        }
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

    onTesisChanged(): void {
        this.pageNumber = 1;
        this.loadPaged();
    }

    onDurumChanged(): void {
        this.pageNumber = 1;
        this.loadPaged();
    }

    onPageChange(event: PaginatorState): void {
        const nextRows = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextPage = event.page !== undefined && event.page >= 0 ? event.page + 1 : this.pageNumber;
        this.pageSize = nextRows;
        this.pageNumber = nextPage;
        this.loadPaged();
    }

    refresh(): void {
        this.loadPaged();
    }

    openCardMenu(event: MouseEvent, item: OdaTemizlikKayitDto): void {
        if (!this.canManage || this.actionLoading) {
            return;
        }

        const anchor = (event.currentTarget as HTMLElement | null) ?? (event.target as HTMLElement | null);
        if (!anchor) {
            return;
        }

        this.selectedKayit = item;
        this.rowActionItems = this.buildRowActionItems(item);
        this.cdr.detectChanges();

        if (!this.cardMenu) {
            return;
        }

        const isOverlayVisible = (this.cardMenu as unknown as { overlayVisible?: boolean }).overlayVisible === true;
        if (isOverlayVisible) {
            this.cardMenu.hide();
            if (this.menuReopenHandle !== null) {
                clearTimeout(this.menuReopenHandle);
            }

            this.menuReopenHandle = setTimeout(() => {
                this.menuReopenHandle = null;
                this.cardMenu?.show(this.createMenuAnchorEvent(anchor));
            }, 0);

            return;
        }

        this.cardMenu.show(this.createMenuAnchorEvent(anchor));
    }

    getDurumSeverity(durum: string): 'danger' | 'warn' | 'success' | 'secondary' {
        if (durum === 'Kirli') {
            return UiSeverity.Danger;
        }

        if (durum === 'Temizleniyor') {
            return UiSeverity.Warn;
        }

        if (durum === 'Hazir') {
            return UiSeverity.Success;
        }

        return UiSeverity.Secondary;
    }

    getRoomImageClass(item: OdaTemizlikKayitDto): string {
        if (item.temizlikDurumu === 'Kirli') {
            return 'room-dirty';
        }

        if (item.temizlikDurumu === 'Temizleniyor') {
            return 'room-cleaning';
        }

        return 'room-ready';
    }

    getRoomImageUrl(item: OdaTemizlikKayitDto): string {
        void item;
        return this.roomImageUrl;
    }

    onRoomImageError(event: Event): void {
        const image = event.target as HTMLImageElement | null;
        if (!image) {
            return;
        }

        if (image.dataset['fallbackApplied'] === '1') {
            return;
        }

        image.dataset['fallbackApplied'] = '1';
        image.src = 'demo/images/product/product-placeholder.svg';
    }

    private buildRowActionItems(item: OdaTemizlikKayitDto): MenuItem[] {
        const items: MenuItem[] = [];

        if (item.temizlikDurumu === 'Kirli') {
            items.push({
                label: 'Temizlige Basla',
                icon: 'pi pi-play',
                command: () => this.baslatTemizlik(item)
            });
        }

        if (item.temizlikDurumu === 'Temizleniyor') {
            items.push({
                label: 'Temizligi Tamamla',
                icon: 'pi pi-check',
                command: () => this.tamamlaTemizlik(item)
            });
        }

        if (items.length === 0) {
            items.push({
                label: 'Islem Yok',
                icon: 'pi pi-ban',
                disabled: true
            });
        }

        return items;
    }

    private baslatTemizlik(item: OdaTemizlikKayitDto): void {
        if (this.actionLoading) {
            return;
        }

        this.actionLoading = true;
        this.service
            .baslatTemizlik(item.odaId)
            .pipe(
                finalize(() => {
                    this.actionLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: `${item.odaNo} icin temizlik baslatildi.` });
                    this.loadPaged();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private tamamlaTemizlik(item: OdaTemizlikKayitDto): void {
        if (this.actionLoading) {
            return;
        }

        this.actionLoading = true;
        this.service
            .tamamlaTemizlik(item.odaId)
            .pipe(
                finalize(() => {
                    this.actionLoading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: `${item.odaNo} oda hazir durumuna alindi.` });
                    this.loadPaged();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadInitialData(): void {
        if (!this.canView) {
            return;
        }

        this.loading = true;
        this.service
            .getTesisler()
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (tesisler) => {
                    this.tesisOptions = tesisler
                        .map((x) => ({ label: x.ad, value: x.id }))
                        .sort((a, b) => a.label.localeCompare(b.label));

                    if (!this.selectedTesisId && this.tesisOptions.length > 0) {
                        this.selectedTesisId = this.tesisOptions[0].value;
                    }

                    this.loadPaged();
                },
                error: (error: unknown) => {
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
            .getPaged(
                this.pageNumber,
                this.pageSize,
                this.searchQuery,
                this.sortBy,
                this.sortDir,
                this.selectedTesisId,
                this.selectedDurum
            )
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

    private createMenuAnchorEvent(anchor: HTMLElement): { target: HTMLElement; currentTarget: HTMLElement } {
        return {
            target: anchor,
            currentTarget: anchor
        };
    }
}
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';

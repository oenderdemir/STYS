import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    CreateRestoranGlobalMenuKategoriRequest,
    RestoranGlobalMenuKategoriModel,
    RestoranModel,
    SaveRestoranKategoriAtamaRequest,
    UpdateRestoranGlobalMenuKategoriRequest
} from './restoran-yonetimi.dto';
import { RestoranMenuYonetimiService } from './restoran-menu-yonetimi.service';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

@Component({
    selector: 'app-restoran-kategori-havuzu-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, MultiSelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './restoran-kategori-havuzu-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RestoranKategoriHavuzuYonetimi implements OnInit {
    private readonly restoranService = inject(RestoranYonetimiService);
    private readonly menuService = inject(RestoranMenuYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    atamaSaving = false;
    kategoriDialogVisible = false;
    kategoriDialogMode: 'create' | 'edit' = 'create';

    restoranlar: RestoranModel[] = [];
    globalKategoriler: RestoranGlobalMenuKategoriModel[] = [];
    selectedRestoranId: number | null = null;
    seciliGlobalKategoriIdleri: number[] = [];

    kategoriModel: RestoranGlobalMenuKategoriModel = this.createEmptyKategori();

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranMenuYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadInitial();
    }

    refresh(): void {
        this.loadInitial();
    }

    openNewKategori(): void {
        if (!this.canManage) {
            return;
        }

        this.kategoriDialogMode = 'create';
        this.kategoriModel = this.createEmptyKategori();
        this.kategoriDialogVisible = true;
    }

    openEditKategori(item: RestoranGlobalMenuKategoriModel): void {
        if (!this.canManage) {
            return;
        }

        this.kategoriDialogMode = 'edit';
        this.kategoriModel = { ...item };
        this.kategoriDialogVisible = true;
    }

    saveKategori(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (!this.kategoriModel.ad.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kategori adi zorunludur.' });
            return;
        }

        const payload: CreateRestoranGlobalMenuKategoriRequest | UpdateRestoranGlobalMenuKategoriRequest = {
            ad: this.kategoriModel.ad.trim(),
            siraNo: this.kategoriModel.siraNo,
            aktifMi: this.kategoriModel.aktifMi
        };

        this.saving = true;
        const request$ = this.kategoriDialogMode === 'edit'
            ? this.menuService.updateGlobalKategori(this.kategoriModel.id, payload as UpdateRestoranGlobalMenuKategoriRequest)
            : this.menuService.createGlobalKategori(payload as CreateRestoranGlobalMenuKategoriRequest);

        request$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.kategoriDialogVisible = false;
                    this.loadGlobalKategoriler();
                    if (this.selectedRestoranId) {
                        this.loadAtamaBaglam(this.selectedRestoranId);
                    }
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Global kategori kaydedildi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    deleteKategori(item: RestoranGlobalMenuKategoriModel): void {
        if (!this.canManage) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.ad}" global kategorisi pasiflestirilsin mi?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.menuService.deleteGlobalKategori(item.id).subscribe({
                    next: () => {
                        this.loadGlobalKategoriler();
                        if (this.selectedRestoranId) {
                            this.loadAtamaBaglam(this.selectedRestoranId);
                        }
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Global kategori pasiflestirildi.' });
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    }
                });
            }
        });
    }

    onRestoranChange(): void {
        if (!this.selectedRestoranId) {
            this.seciliGlobalKategoriIdleri = [];
            return;
        }

        this.loadAtamaBaglam(this.selectedRestoranId);
    }

    saveAtamalar(): void {
        if (!this.canManage || !this.selectedRestoranId) {
            return;
        }

        this.atamaSaving = true;
        const payload: SaveRestoranKategoriAtamaRequest = {
            restoranId: this.selectedRestoranId,
            seciliGlobalKategoriIdleri: [...this.seciliGlobalKategoriIdleri]
        };

        this.menuService.saveKategoriAtamalari(payload)
            .pipe(finalize(() => {
                this.atamaSaving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.seciliGlobalKategoriIdleri = [...baglam.seciliGlobalKategoriIdleri];
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kategori atamalari kaydedildi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadInitial(): void {
        this.loading = true;
        forkJoin({
            restoranlar: this.restoranService.getAll(),
            kategoriler: this.menuService.getGlobalKategoriler()
        })
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ restoranlar, kategoriler }) => {
                    this.restoranlar = restoranlar;
                    this.globalKategoriler = kategoriler;
                    if (!this.selectedRestoranId && this.restoranlar.length > 0) {
                        this.selectedRestoranId = this.restoranlar[0].id ?? null;
                    }
                    if (this.selectedRestoranId) {
                        this.loadAtamaBaglam(this.selectedRestoranId);
                    }
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadGlobalKategoriler(): void {
        this.menuService.getGlobalKategoriler().subscribe({
            next: (items) => {
                this.globalKategoriler = items;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    private loadAtamaBaglam(restoranId: number): void {
        this.menuService.getKategoriAtamaBaglam(restoranId).subscribe({
            next: (baglam) => {
                this.seciliGlobalKategoriIdleri = [...baglam.seciliGlobalKategoriIdleri];
                this.globalKategoriler = baglam.globalKategoriler;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
            }
        });
    }

    private createEmptyKategori(): RestoranGlobalMenuKategoriModel {
        return {
            id: 0,
            ad: '',
            siraNo: 0,
            aktifMi: true,
            restoranSayisi: 0
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

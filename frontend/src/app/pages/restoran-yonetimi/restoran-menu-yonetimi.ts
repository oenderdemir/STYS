import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    CreateRestoranMenuKategoriRequest,
    CreateRestoranMenuUrunRequest,
    PARA_BIRIMI_SECENEKLERI,
    RestoranMenuKategoriModel,
    RestoranMenuUrunModel,
    RestoranModel,
    UpdateRestoranMenuKategoriRequest,
    UpdateRestoranMenuUrunRequest
} from './restoran-yonetimi.dto';
import { RestoranMenuYonetimiService } from './restoran-menu-yonetimi.service';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

@Component({
    selector: 'app-restoran-menu-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './restoran-menu-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RestoranMenuYonetimi implements OnInit {
    private readonly restoranService = inject(RestoranYonetimiService);
    private readonly service = inject(RestoranMenuYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    savingKategori = false;
    savingUrun = false;

    restoranlar: RestoranModel[] = [];
    selectedRestoranId: number | null = null;

    kategoriler: RestoranMenuKategoriModel[] = [];
    selectedKategoriId: number | null = null;
    urunMap: Record<number, RestoranMenuUrunModel[]> = {};

    kategoriDialogVisible = false;
    kategoriDialogMode: 'create' | 'edit' = 'create';
    kategoriModel: RestoranMenuKategoriModel = this.createEmptyKategori();

    urunDialogVisible = false;
    urunDialogMode: 'create' | 'edit' = 'create';
    urunModel: RestoranMenuUrunModel = this.createEmptyUrun();

    readonly paraBirimiSecenekleri = [...PARA_BIRIMI_SECENEKLERI];

    get selectedKategoriUrunleri(): RestoranMenuUrunModel[] {
        if (!this.selectedKategoriId) {
            return [];
        }

        return this.urunMap[this.selectedKategoriId] ?? [];
    }

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranMenuYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadRestoranlar();
    }

    onRestoranChange(): void {
        this.loadMenu();
    }

    refresh(): void {
        this.loadMenu();
    }

    selectKategori(kategoriId: number): void {
        this.selectedKategoriId = kategoriId;
    }

    openNewKategori(): void {
        if (!this.canManage || !this.selectedRestoranId) {
            return;
        }

        this.kategoriDialogMode = 'create';
        this.kategoriModel = this.createEmptyKategori();
        this.kategoriModel.restoranId = this.selectedRestoranId;
        this.kategoriDialogVisible = true;
    }

    openEditKategori(item: RestoranMenuKategoriModel): void {
        if (!this.canManage) {
            return;
        }

        this.kategoriDialogMode = 'edit';
        this.kategoriModel = { ...item };
        this.kategoriDialogVisible = true;
    }

    saveKategori(): void {
        if (!this.canManage || this.savingKategori) {
            return;
        }

        if (!this.kategoriModel.restoranId || !this.kategoriModel.ad.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kategori adi zorunludur.' });
            return;
        }

        const payload: CreateRestoranMenuKategoriRequest | UpdateRestoranMenuKategoriRequest = {
            restoranId: this.kategoriModel.restoranId,
            ad: this.kategoriModel.ad.trim(),
            siraNo: this.kategoriModel.siraNo,
            aktifMi: this.kategoriModel.aktifMi
        };

        this.savingKategori = true;
        const request$ = this.kategoriDialogMode === 'edit' && this.kategoriModel.id
            ? this.service.updateKategori(this.kategoriModel.id, payload as UpdateRestoranMenuKategoriRequest)
            : this.service.createKategori(payload as CreateRestoranMenuKategoriRequest);

        request$
            .pipe(finalize(() => {
                this.savingKategori = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.kategoriDialogVisible = false;
                    this.loadMenu();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kategori kaydedildi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    deleteKategori(item: RestoranMenuKategoriModel): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.ad}" kategorisi silinsin mi?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            accept: () => {
                this.service.deleteKategori(item.id!).subscribe({
                    next: () => {
                        this.loadMenu();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kategori silindi.' });
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    }
                });
            }
        });
    }

    openNewUrun(): void {
        if (!this.canManage || !this.selectedKategoriId) {
            return;
        }

        this.urunDialogMode = 'create';
        this.urunModel = this.createEmptyUrun();
        this.urunModel.restoranMenuKategoriId = this.selectedKategoriId;
        this.urunDialogVisible = true;
    }

    openEditUrun(item: RestoranMenuUrunModel): void {
        if (!this.canManage) {
            return;
        }

        this.urunDialogMode = 'edit';
        this.urunModel = { ...item };
        this.urunDialogVisible = true;
    }

    saveUrun(): void {
        if (!this.canManage || this.savingUrun) {
            return;
        }

        if (!this.urunModel.restoranMenuKategoriId || !this.urunModel.ad.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Urun adi zorunludur.' });
            return;
        }

        const payload: CreateRestoranMenuUrunRequest | UpdateRestoranMenuUrunRequest = {
            restoranMenuKategoriId: this.urunModel.restoranMenuKategoriId,
            ad: this.urunModel.ad.trim(),
            aciklama: this.urunModel.aciklama?.trim() || null,
            fiyat: this.urunModel.fiyat,
            paraBirimi: this.urunModel.paraBirimi,
            hazirlamaSuresiDakika: this.urunModel.hazirlamaSuresiDakika,
            aktifMi: this.urunModel.aktifMi
        };

        this.savingUrun = true;
        const request$ = this.urunDialogMode === 'edit' && this.urunModel.id
            ? this.service.updateUrun(this.urunModel.id, payload as UpdateRestoranMenuUrunRequest)
            : this.service.createUrun(payload as CreateRestoranMenuUrunRequest);

        request$
            .pipe(finalize(() => {
                this.savingUrun = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.urunDialogVisible = false;
                    this.loadMenu();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Urun kaydedildi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    deleteUrun(item: RestoranMenuUrunModel): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.ad}" urunu silinsin mi?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            accept: () => {
                this.service.deleteUrun(item.id!).subscribe({
                    next: () => {
                        this.loadMenu();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Urun silindi.' });
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    }
                });
            }
        });
    }

    private loadRestoranlar(): void {
        this.loading = true;
        this.restoranService.getAll()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.restoranlar = items;
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadMenu(): void {
        if (!this.selectedRestoranId) {
            this.kategoriler = [];
            this.urunMap = {};
            this.selectedKategoriId = null;
            return;
        }

        this.loading = true;
        this.service.getMenuByRestoranId(this.selectedRestoranId)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (response) => {
                    this.kategoriler = response.kategoriler;
                    this.urunMap = response.urunMap;
                    if (!this.selectedKategoriId || !this.kategoriler.some((x) => x.id === this.selectedKategoriId)) {
                        this.selectedKategoriId = this.kategoriler.length > 0 ? this.kategoriler[0].id ?? null : null;
                    }
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private createEmptyKategori(): RestoranMenuKategoriModel {
        return {
            restoranId: 0,
            ad: '',
            siraNo: 0,
            aktifMi: true
        };
    }

    private createEmptyUrun(): RestoranMenuUrunModel {
        return {
            restoranMenuKategoriId: 0,
            ad: '',
            aciklama: null,
            fiyat: 0,
            paraBirimi: 'TRY',
            hazirlamaSuresiDakika: 0,
            aktifMi: true
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

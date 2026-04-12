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
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { PanelModule } from 'primeng/panel';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    CreateRestoranMenuUrunRequest,
    PARA_BIRIMI_SECENEKLERI,
    RestoranMenuKategoriModel,
    RestoranMenuUrunModel,
    RestoranModel,
    UpdateRestoranMenuUrunRequest
} from './restoran-yonetimi.dto';
import { RestoranMenuYonetimiService } from './restoran-menu-yonetimi.service';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

interface RestoranMenuKategoriGrupModel {
    id: number;
    ad: string;
    siraNo: number;
    urunler: RestoranMenuUrunModel[];
}

@Component({
    selector: 'app-restoran-menu-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, InputGroupModule, InputGroupAddonModule, PanelModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
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
    savingUrun = false;
    urunDialogVisible = false;
    urunDialogMode: 'create' | 'edit' = 'create';
    urunAramaMetni = '';

    restoranlar: RestoranModel[] = [];
    kategoriler: RestoranMenuKategoriModel[] = [];
    kategoriGruplari: RestoranMenuKategoriGrupModel[] = [];
    urunMap: Record<number, RestoranMenuUrunModel[]> = {};
    selectedRestoranId: number | null = null;

    urunModel: RestoranMenuUrunModel = this.createEmptyUrun();
    readonly paraBirimiSecenekleri = [...PARA_BIRIMI_SECENEKLERI];

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranMenuYonetimi.Manage');
    }

    get hasKategori(): boolean {
        return this.kategoriler.length > 0;
    }

    ngOnInit(): void {
        this.loadInitial();
    }

    refresh(): void {
        this.loadInitial();
    }

    onRestoranChange(): void {
        this.loadMenu();
    }

    openNewUrun(kategoriId?: number): void {
        if (!this.canManage || !this.selectedRestoranId) {
            return;
        }

        if (!this.hasKategori) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Urun Eklenemedi', detail: 'Bu restorana urun eklemek icin once kategori atamasi yapilmalidir.' });
            return;
        }

        this.urunDialogMode = 'create';
        this.urunModel = this.createEmptyUrun();
        this.urunModel.restoranMenuKategoriId = kategoriId ?? (this.kategoriler[0]?.id ?? 0);
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

        if (!this.urunModel.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Urun adi zorunludur.' });
            return;
        }

        if (!this.urunModel.restoranMenuKategoriId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Urun kategorisi bulunamadi.' });
            return;
        }

        const payload: CreateRestoranMenuUrunRequest | UpdateRestoranMenuUrunRequest = {
            restoranMenuKategoriId: this.urunModel.restoranMenuKategoriId,
            ad: this.urunModel.ad.trim(),
            aciklama: this.urunModel.aciklama?.trim() ?? null,
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
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: this.urunDialogMode === 'edit' ? 'Urun guncellendi.' : 'Urun olusturuldu.'
                    });
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
            message: `"${item.ad}" urununu silmek istiyor musunuz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
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

    getFiltrelenmisUrunler(grup: RestoranMenuKategoriGrupModel): RestoranMenuUrunModel[] {
        const arama = this.urunAramaMetni?.trim().toLocaleLowerCase('tr-TR');
        if (!arama) {
            return grup.urunler;
        }

        return grup.urunler.filter((urun) => {
            const ad = (urun.ad ?? '').toLocaleLowerCase('tr-TR');
            const aciklama = (urun.aciklama ?? '').toLocaleLowerCase('tr-TR');
            return ad.includes(arama) || aciklama.includes(arama);
        });
    }

    private loadInitial(): void {
        this.loading = true;
        forkJoin({
            restoranlar: this.restoranService.getAll()
        })
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ restoranlar }) => {
                    this.restoranlar = restoranlar;
                    if (!this.selectedRestoranId && this.restoranlar.length > 0) {
                        this.selectedRestoranId = this.restoranlar[0].id ?? null;
                    }
                    this.loadMenu();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadMenu(): void {
        if (!this.selectedRestoranId) {
            this.kategoriler = [];
            this.kategoriGruplari = [];
            this.urunMap = {};
            return;
        }

        this.loading = true;
        this.service.getYonetimMenuByRestoranId(this.selectedRestoranId)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ kategoriler, urunMap }) => {
                    this.kategoriler = [...kategoriler].sort((a, b) => (a.siraNo - b.siraNo) || a.ad.localeCompare(b.ad, 'tr'));
                    this.urunMap = urunMap;
                    this.kategoriGruplari = this.kategoriler.map((kategori) => ({
                        id: kategori.id ?? 0,
                        ad: kategori.ad,
                        siraNo: kategori.siraNo,
                        urunler: [...(this.urunMap[kategori.id ?? 0] ?? [])].sort((a, b) => a.ad.localeCompare(b.ad, 'tr'))
                    }));
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private createEmptyUrun(): RestoranMenuUrunModel {
        return {
            restoranMenuKategoriId: 0,
            ad: '',
            aciklama: '',
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

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { ManagerCandidateDto } from '../../core/identity';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { CreateRestoranRequest, RestoranIsletmeAlaniSecenekModel, RestoranModel, TesisSecenekModel, UpdateRestoranRequest } from './restoran-yonetimi.dto';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

@Component({
    selector: 'app-restoran-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './restoran-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RestoranYonetimi implements OnInit {
    private readonly service = inject(RestoranYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    tesisler: TesisSecenekModel[] = [];
    isletmeAlanlari: RestoranIsletmeAlaniSecenekModel[] = [];
    yoneticiAdaylari: ManagerCandidateDto[] = [];
    restoranlar: RestoranModel[] = [];
    selectedTesisId: number | null = null;

    model: RestoranModel = this.createEmptyModel();

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadData();
    }

    refresh(): void {
        this.loadData();
    }

    onTesisFilterChange(): void {
        this.loadRestoranlar();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.dialogMode = 'create';
        this.model = this.createEmptyModel();
        if (this.selectedTesisId) {
            this.model.tesisId = this.selectedTesisId;
            this.loadIsletmeAlanlariForTesis(this.selectedTesisId);
        } else {
            this.isletmeAlanlari = [];
        }
        this.dialogVisible = true;
    }

    openEdit(item: RestoranModel): void {
        if (!this.canManage) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = {
            id: item.id,
            tesisId: item.tesisId,
            isletmeAlaniId: item.isletmeAlaniId ?? null,
            yoneticiUserIds: item.yoneticiUserIds ? [...item.yoneticiUserIds] : [],
            ad: item.ad,
            aciklama: item.aciklama ?? null,
            aktifMi: item.aktifMi
        };
        this.loadIsletmeAlanlariForTesis(item.tesisId);
        this.dialogVisible = true;
    }

    onDialogTesisChange(): void {
        if (!this.model.tesisId || this.model.tesisId <= 0) {
            this.model.isletmeAlaniId = null;
            this.isletmeAlanlari = [];
            return;
        }

        this.model.isletmeAlaniId = null;
        this.loadIsletmeAlanlariForTesis(this.model.tesisId);
    }

    save(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (this.model.tesisId <= 0 || !this.model.ad.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tesis ve restoran adi zorunludur.' });
            return;
        }

        const payload: CreateRestoranRequest | UpdateRestoranRequest = {
            tesisId: this.model.tesisId,
            isletmeAlaniId: this.model.isletmeAlaniId ?? null,
            yoneticiUserIds: this.model.yoneticiUserIds ?? [],
            ad: this.model.ad.trim(),
            aciklama: this.model.aciklama?.trim() || null,
            aktifMi: this.model.aktifMi
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateRestoranRequest)
            : this.service.create(payload as CreateRestoranRequest);

        request$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadRestoranlar();
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: this.dialogMode === 'edit' ? 'Restoran guncellendi.' : 'Restoran olusturuldu.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    delete(item: RestoranModel): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.ad}" restoranini silmek istiyor musunuz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.loadRestoranlar();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Restoran silindi.' });
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

    getTesisAdi(tesisId: number): string {
        return this.tesisler.find((x) => x.id === tesisId)?.ad ?? '-';
    }

    getIsletmeAlaniAdi(item: RestoranModel): string {
        if (item.isletmeAlaniAdi && item.isletmeAlaniAdi.trim().length > 0) {
            return item.isletmeAlaniAdi;
        }

        return '-';
    }

    formatYoneticiLabel(aday: ManagerCandidateDto): string {
        const adSoyad = aday.adSoyad?.trim();
        if (adSoyad) {
            return `${adSoyad} (${aday.userName})`;
        }

        return aday.userName;
    }

    get yoneticiSecenekleri(): Array<{ label: string; value: string }> {
        return this.yoneticiAdaylari.map((item) => ({
            value: item.id,
            label: this.formatYoneticiLabel(item)
        }));
    }

    getYoneticiSayisi(item: RestoranModel): number {
        return item.yoneticiUserIds?.length ?? 0;
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            tesisler: this.service.getTesisler(),
            restoranlar: this.service.getAll(this.selectedTesisId),
            yoneticiAdaylari: this.canManage ? this.service.getYoneticiAdaylari() : of([])
        })
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ tesisler, restoranlar, yoneticiAdaylari }) => {
                    this.tesisler = [...tesisler].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.restoranlar = restoranlar;
                    this.yoneticiAdaylari = yoneticiAdaylari;
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadRestoranlar(): void {
        this.loading = true;
        this.service.getAll(this.selectedTesisId)
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

    private createEmptyModel(): RestoranModel {
        return {
            tesisId: 0,
            isletmeAlaniId: null,
            yoneticiUserIds: [],
            ad: '',
            aciklama: null,
            aktifMi: true
        };
    }

    private loadIsletmeAlanlariForTesis(tesisId: number): void {
        if (!tesisId || tesisId <= 0) {
            this.isletmeAlanlari = [];
            return;
        }

        this.service.getIsletmeAlanlariByTesisId(tesisId).subscribe({
            next: (items) => {
                this.isletmeAlanlari = items;
                const seciliId = this.model.isletmeAlaniId;
                if (seciliId && !items.some((x) => x.id === seciliId)) {
                    this.model.isletmeAlaniId = null;
                }
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.isletmeAlanlari = [];
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
}

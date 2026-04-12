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
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import {
    CreateRestoranMasaRequest,
    getMasaDurumSeverity,
    RESTORAN_MASA_DURUMLARI,
    RestoranMasaModel,
    RestoranModel,
    UpdateRestoranMasaRequest
} from './restoran-yonetimi.dto';
import { RestoranMasaYonetimiService } from './restoran-masa-yonetimi.service';
import { RestoranYonetimiService } from './restoran-yonetimi.service';

@Component({
    selector: 'app-restoran-masa-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './restoran-masa-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RestoranMasaYonetimi implements OnInit {
    private readonly restoranService = inject(RestoranYonetimiService);
    private readonly service = inject(RestoranMasaYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    restoranlar: RestoranModel[] = [];
    masalar: RestoranMasaModel[] = [];
    selectedRestoranId: number | null = null;

    model: RestoranMasaModel = this.createEmptyModel();

    readonly masaDurumlari = [...RESTORAN_MASA_DURUMLARI];

    get canManage(): boolean {
        return this.authService.hasPermission('RestoranMasaYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadInitial();
    }

    refresh(): void {
        this.loadMasalar();
    }

    onRestoranChange(): void {
        this.loadMasalar();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.dialogMode = 'create';
        this.model = this.createEmptyModel();
        if (this.selectedRestoranId) {
            this.model.restoranId = this.selectedRestoranId;
        }
        this.dialogVisible = true;
    }

    openEdit(item: RestoranMasaModel): void {
        if (!this.canManage) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        if (this.model.restoranId <= 0 || !this.model.masaNo.trim() || this.model.kapasite <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Restoran, masa no ve kapasite zorunludur.' });
            return;
        }

        const payload: CreateRestoranMasaRequest | UpdateRestoranMasaRequest = {
            restoranId: this.model.restoranId,
            masaNo: this.model.masaNo.trim(),
            kapasite: this.model.kapasite,
            durum: this.model.durum,
            aktifMi: this.model.aktifMi
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateRestoranMasaRequest)
            : this.service.create(payload as CreateRestoranMasaRequest);

        request$
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.loadMasalar();
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: this.dialogMode === 'edit' ? 'Masa guncellendi.' : 'Masa olusturuldu.'
                    });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    delete(item: RestoranMasaModel): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.masaNo}" masasini silmek istiyor musunuz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.loadMasalar();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Masa silindi.' });
                    },
                    error: (error: unknown) => {
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    }
                });
            }
        });
    }

    getRestoranAdi(restoranId: number): string {
        return this.restoranlar.find((x) => x.id === restoranId)?.ad ?? '-';
    }

    getMasaDurumSeverityValue(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        return getMasaDurumSeverity(durum);
    }

    private loadInitial(): void {
        this.loading = true;
        forkJoin({
            restoranlar: this.restoranService.getAll(),
            masalar: this.service.getAll()
        })
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ restoranlar, masalar }) => {
                    this.restoranlar = restoranlar;
                    this.masalar = masalar;
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private loadMasalar(): void {
        this.loading = true;
        const request$ = this.selectedRestoranId
            ? this.service.getByRestoranId(this.selectedRestoranId)
            : this.service.getAll();

        request$
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.masalar = items;
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private createEmptyModel(): RestoranMasaModel {
        return {
            restoranId: 0,
            masaNo: '',
            kapasite: 1,
            durum: 'Musait',
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

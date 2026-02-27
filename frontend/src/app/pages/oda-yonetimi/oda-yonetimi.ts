import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { Table, TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.service';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.service';
import { OdaDto, OdaYonetimiService } from './oda-yonetimi.service';

@Component({
    selector: 'app-oda-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './oda-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class OdaYonetimi implements OnInit {
    private readonly service = inject(OdaYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    odalar: OdaDto[] = [];
    binalar: BinaDto[] = [];
    odaTipleri: OdaTipiDto[] = [];
    selectedOda: OdaDto = this.getEmptyOda();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('OdaYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadData();
    }

    onGlobalFilter(table: Table, event: Event): void {
        table.filterGlobal((event.target as HTMLInputElement).value, 'contains');
    }

    refresh(): void {
        this.loadData();
    }

    openNew(): void {
        this.selectedOda = this.getEmptyOda();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(oda: OdaDto): void {
        this.selectedOda = { ...oda };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveOda(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: OdaDto = {
            odaNo: this.selectedOda.odaNo.trim(),
            binaId: this.selectedOda.binaId,
            odaTipiId: this.selectedOda.odaTipiId,
            katNo: this.selectedOda.katNo,
            yatakSayisi: this.selectedOda.yatakSayisi ?? null,
            aktifMi: this.selectedOda.aktifMi
        };

        if (!payload.odaNo || !payload.binaId || !payload.odaTipiId) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Oda no, bina ve oda tipi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedOda.id ? this.service.updateOda(this.selectedOda.id, payload) : this.service.createOda(payload);

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
                    this.loadData();
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Oda guncellendi.' : 'Oda olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteOda(oda: OdaDto): void {
        if (!this.canManage || !oda.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${oda.odaNo}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteOda(oda.id!).subscribe({
                    next: () => {
                        this.loadData();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda silindi.' });
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

    getBinaAdi(binaId: number): string {
        const bina = this.binalar.find((x) => x.id === binaId);
        return bina?.ad ?? '-';
    }

    getOdaTipiAdi(odaTipiId: number): string {
        const odaTipi = this.odaTipleri.find((x) => x.id === odaTipiId);
        return odaTipi?.ad ?? '-';
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            odalar: this.service.getOdalar(),
            binalar: this.service.getBinalar(),
            odaTipleri: this.service.getOdaTipleri()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ odalar, binalar, odaTipleri }) => {
                    this.odalar = [...odalar].sort((left, right) => (left.odaNo ?? '').localeCompare(right.odaNo ?? ''));
                    this.binalar = [...binalar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.odaTipleri = [...odaTipleri].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyOda(): OdaDto {
        return {
            odaNo: '',
            binaId: 0,
            odaTipiId: 0,
            katNo: 0,
            yatakSayisi: null,
            aktifMi: true
        };
    }
}

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { Table, TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { OdaTipiDto, OdaTipiYonetimiService } from './oda-tipi-yonetimi.service';

@Component({
    selector: 'app-oda-tipi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './oda-tipi-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class OdaTipiYonetimi implements OnInit {
    private readonly service = inject(OdaTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    odaTipleri: OdaTipiDto[] = [];
    selectedOdaTipi: OdaTipiDto = this.getEmptyOdaTipi();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('OdaTipiYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadOdaTipleri();
    }

    onGlobalFilter(table: Table, event: Event): void {
        table.filterGlobal((event.target as HTMLInputElement).value, 'contains');
    }

    refresh(): void {
        this.loadOdaTipleri();
    }

    openNew(): void {
        this.selectedOdaTipi = this.getEmptyOdaTipi();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(odaTipi: OdaTipiDto): void {
        this.selectedOdaTipi = { ...odaTipi };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveOdaTipi(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: OdaTipiDto = {
            ad: this.selectedOdaTipi.ad.trim(),
            paylasimliMi: this.selectedOdaTipi.paylasimliMi,
            kapasite: this.selectedOdaTipi.kapasite,
            balkonVarMi: this.selectedOdaTipi.balkonVarMi,
            klimaVarMi: this.selectedOdaTipi.klimaVarMi,
            metrekare: this.selectedOdaTipi.metrekare ?? null,
            aktifMi: this.selectedOdaTipi.aktifMi
        };

        if (!payload.ad || payload.kapasite <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ad ve pozitif kapasite zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedOdaTipi.id ? this.service.updateOdaTipi(this.selectedOdaTipi.id, payload) : this.service.createOdaTipi(payload);

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
                    this.loadOdaTipleri();
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Oda tipi guncellendi.' : 'Oda tipi olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteOdaTipi(odaTipi: OdaTipiDto): void {
        if (!this.canManage || !odaTipi.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${odaTipi.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteOdaTipi(odaTipi.id!).subscribe({
                    next: () => {
                        this.loadOdaTipleri();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda tipi silindi.' });
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

    private loadOdaTipleri(): void {
        this.loading = true;
        this.service
            .getOdaTipleri()
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (odaTipleri) => {
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

    private getEmptyOdaTipi(): OdaTipiDto {
        return {
            ad: '',
            paylasimliMi: false,
            kapasite: 1,
            balkonVarMi: false,
            klimaVarMi: false,
            metrekare: null,
            aktifMi: true
        };
    }
}

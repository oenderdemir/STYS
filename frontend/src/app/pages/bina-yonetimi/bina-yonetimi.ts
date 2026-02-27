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
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.service';
import { BinaDto, BinaYonetimiService } from './bina-yonetimi.service';

@Component({
    selector: 'app-bina-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './bina-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class BinaYonetimi implements OnInit {
    private readonly service = inject(BinaYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    binalar: BinaDto[] = [];
    tesisler: TesisDto[] = [];
    selectedBina: BinaDto = this.getEmptyBina();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('BinaYonetimi.Manage');
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
        this.selectedBina = this.getEmptyBina();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(bina: BinaDto): void {
        this.selectedBina = { ...bina };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveBina(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: BinaDto = {
            ad: this.selectedBina.ad.trim(),
            tesisId: this.selectedBina.tesisId,
            katSayisi: this.selectedBina.katSayisi,
            aktifMi: this.selectedBina.aktifMi
        };

        if (!payload.ad || !payload.tesisId || payload.katSayisi <= 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Ad, tesis ve pozitif kat sayisi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedBina.id ? this.service.updateBina(this.selectedBina.id, payload) : this.service.createBina(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Bina guncellendi.' : 'Bina olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteBina(bina: BinaDto): void {
        if (!this.canManage || !bina.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${bina.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteBina(bina.id!).subscribe({
                    next: () => {
                        this.loadData();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Bina silindi.' });
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

    getTesisAdi(tesisId: number): string {
        const tesis = this.tesisler.find((x) => x.id === tesisId);
        return tesis?.ad ?? '-';
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            binalar: this.service.getBinalar(),
            tesisler: this.service.getTesisler()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ binalar, tesisler }) => {
                    this.binalar = [...binalar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyBina(): BinaDto {
        return {
            ad: '',
            tesisId: 0,
            katSayisi: 1,
            aktifMi: true
        };
    }
}

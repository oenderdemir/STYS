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
import { InputTextModule } from 'primeng/inputtext';
import { Table, TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { IlDto, IlYonetimiService } from './il-yonetimi.service';

@Component({
    selector: 'app-il-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './il-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IlYonetimi implements OnInit {
    private readonly service = inject(IlYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    iller: IlDto[] = [];
    selectedIl: IlDto = this.getEmptyIl();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('IlYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadIller();
    }

    onGlobalFilter(table: Table, event: Event): void {
        table.filterGlobal((event.target as HTMLInputElement).value, 'contains');
    }

    refresh(): void {
        this.loadIller();
    }

    openNew(): void {
        this.selectedIl = this.getEmptyIl();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(il: IlDto): void {
        this.selectedIl = { ...il };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveIl(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: IlDto = {
            ad: this.selectedIl.ad.trim(),
            aktifMi: this.selectedIl.aktifMi
        };

        if (!payload.ad) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Il adi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedIl.id ? this.service.updateIl(this.selectedIl.id, payload) : this.service.createIl(payload);

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
                    this.loadIller();
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Il guncellendi.' : 'Il olusturuldu.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteIl(il: IlDto): void {
        if (!this.canManage || !il.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${il.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteIl(il.id!).subscribe({
                    next: () => {
                        this.loadIller();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Il silindi.' });
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

    private loadIller(): void {
        this.loading = true;
        this.service
            .getIller()
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (iller) => {
                    this.iller = [...iller].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyIl(): IlDto {
        return {
            ad: '',
            aktifMi: true
        };
    }
}

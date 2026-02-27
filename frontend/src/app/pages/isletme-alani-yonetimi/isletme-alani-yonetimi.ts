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
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { Table, TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.service';
import { IsletmeAlaniDto, IsletmeAlaniYonetimiService } from './isletme-alani-yonetimi.service';

@Component({
    selector: 'app-isletme-alani-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule, ToggleSwitchModule],
    templateUrl: './isletme-alani-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class IsletmeAlaniYonetimi implements OnInit {
    private readonly service = inject(IsletmeAlaniYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    alanlar: IsletmeAlaniDto[] = [];
    binalar: BinaDto[] = [];
    selectedAlan: IsletmeAlaniDto = this.getEmptyAlan();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('IsletmeAlaniYonetimi.Manage');
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
        this.selectedAlan = this.getEmptyAlan();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(alan: IsletmeAlaniDto): void {
        this.selectedAlan = { ...alan };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveAlan(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: IsletmeAlaniDto = {
            ad: this.selectedAlan.ad.trim(),
            binaId: this.selectedAlan.binaId,
            aktifMi: this.selectedAlan.aktifMi
        };

        if (!payload.ad || !payload.binaId) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Alan adi ve bina secimi zorunludur.' });
            return;
        }

        const save$: Observable<unknown> = this.isEditMode && this.selectedAlan.id ? this.service.updateAlan(this.selectedAlan.id, payload) : this.service.createAlan(payload);

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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: this.isEditMode ? 'Isletme alani guncellendi.' : 'Isletme alani olusturuldu.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteAlan(alan: IsletmeAlaniDto): void {
        if (!this.canManage || !alan.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${alan.ad}" kaydini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteAlan(alan.id!).subscribe({
                    next: () => {
                        this.loadData();
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Isletme alani silindi.' });
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

    private loadData(): void {
        this.loading = true;
        forkJoin({
            alanlar: this.service.getAlanlar(),
            binalar: this.service.getBinalar()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ alanlar, binalar }) => {
                    this.alanlar = [...alanlar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    this.binalar = [...binalar].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private getEmptyAlan(): IsletmeAlaniDto {
        return {
            ad: '',
            binaId: 0,
            aktifMi: true
        };
    }
}

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
import { tryReadApiMessage } from '../../core/api';
import { RoleResponseDto } from '../../core/identity';
import { AuthService } from '../auth';
import { RoleRequestDto } from './dto';
import { RolYonetimiService } from './rol-yonetimi.service';

interface RoleEditModel extends RoleRequestDto {
    id?: string | null;
}

@Component({
    selector: 'app-rol-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, IconFieldModule, InputIconModule, InputTextModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './rol-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class RolYonetimi implements OnInit {
    private readonly service = inject(RolYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    roles: RoleResponseDto[] = [];
    selectedRole: RoleEditModel = this.getEmptyRole();
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('RoleManagement.Manage');
    }

    ngOnInit(): void {
        this.loadRoles();
    }

    onGlobalFilter(table: Table, event: Event): void {
        table.filterGlobal((event.target as HTMLInputElement).value, 'contains');
    }

    refresh(): void {
        this.loadRoles();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedRole = this.getEmptyRole();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(role: RoleResponseDto): void {
        this.selectedRole = {
            id: role.id ?? null,
            domain: role.domain ?? '',
            name: role.name ?? ''
        };
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveRole(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const domain = this.selectedRole.domain.trim();
        const name = this.selectedRole.name.trim();
        if (!domain || !name) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Domain ve Ad alanlari zorunludur.'
            });
            return;
        }

        const payload: RoleRequestDto = { domain, name };
        const save$: Observable<unknown> = this.isEditMode && this.selectedRole.id ? this.service.updateRole(this.selectedRole.id, payload) : this.service.createRole(payload);

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
                    this.loadRoles();
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Rol guncellendi.' : 'Rol eklendi.'
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: this.resolveErrorMessage(error)
                    });
                    this.cdr.detectChanges();
                }
            });
    }

    deleteRole(role: RoleResponseDto): void {
        if (!this.canManage) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${role.domain ?? '-'}"."${role.name ?? '-'}" rolunu silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                if (!role.id) {
                    return;
                }

                this.service.deleteRole(role.id).subscribe({
                    next: () => {
                        this.loadRoles();
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Basarili',
                            detail: 'Rol silindi.'
                        });
                        this.cdr.detectChanges();
                    },
                    error: (error: unknown) => {
                        this.messageService.add({
                            severity: 'error',
                            summary: 'Hata',
                            detail: this.resolveErrorMessage(error)
                        });
                        this.cdr.detectChanges();
                    }
                });
            }
        });
    }

    private loadRoles(): void {
        this.loading = true;
        this.service
            .getRoles()
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (roles) => {
                    this.roles = [...roles].sort((left, right) => `${left.domain ?? ''}.${left.name ?? ''}`.localeCompare(`${right.domain ?? ''}.${right.name ?? ''}`));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: this.resolveErrorMessage(error)
                    });
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

    private getEmptyRole(): RoleEditModel {
        return {
            domain: '',
            name: ''
        };
    }
}

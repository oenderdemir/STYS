import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { Table, TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { RoleResponseDto, UserGroupRequestDto, UserGroupResponseDto } from '../../core/identity';
import { AuthService } from '../auth';
import { RolYonetimiService } from '../rol-yonetimi/rol-yonetimi.service';
import { KullaniciGrupYonetimiService } from './kullanici-grup-yonetimi.service';

interface RoleOption {
    id: string;
    label: string;
}

interface UserGroupEditModel {
    id?: string | null;
    name: string;
}

@Component({
    selector: 'app-kullanici-grup-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, DividerModule, IconFieldModule, InputIconModule, InputTextModule, MultiSelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './kullanici-grup-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class KullaniciGrupYonetimi implements OnInit {
    private readonly service = inject(KullaniciGrupYonetimiService);
    private readonly roleService = inject(RolYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    userGroups: UserGroupResponseDto[] = [];
    allRoles: RoleOption[] = [];
    selectedUserGroup: UserGroupEditModel = this.getEmptyUserGroup();
    selectedRoleIds: string[] = [];
    loading = false;
    saving = false;
    dialogVisible = false;
    isEditMode = false;

    get canManage(): boolean {
        return this.authService.hasPermission('UserGroupManagement.Manage') || this.authService.hasPermission('UserManagement.Manage');
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
        this.selectedUserGroup = this.getEmptyUserGroup();
        this.selectedRoleIds = [];
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(group: UserGroupResponseDto): void {
        this.selectedUserGroup = {
            id: group.id ?? null,
            name: group.name
        };
        this.selectedRoleIds = (group.roles ?? []).map((role) => role.id).filter((id): id is string => !!id);
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveUserGroup(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const name = this.selectedUserGroup.name.trim();
        if (!name) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Grup adi zorunludur.'
            });
            return;
        }

        const payload: UserGroupRequestDto = {
            id: this.selectedUserGroup.id ?? null,
            name,
            roles: this.selectedRoleIds.map((roleId) => ({ id: roleId } as RoleResponseDto))
        };

        this.saving = true;
        const save$: Observable<unknown> =
            this.isEditMode && this.selectedUserGroup.id
                ? this.service.updateUserGroup(this.selectedUserGroup.id, payload)
                : this.service.createUserGroup(payload);

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
                    this.loadUserGroups();
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Grup guncellendi.' : 'Yeni grup eklendi.'
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

    deleteUserGroup(group: UserGroupResponseDto): void {
        if (!this.canManage || !group.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${group.name}" grubunu silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteUserGroup(group.id!).subscribe({
                    next: () => {
                        this.loadUserGroups();
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Basarili',
                            detail: 'Grup silindi.'
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

    getRoleText(group: UserGroupResponseDto): string {
        const roleNames = (group.roles ?? [])
            .map((role) => {
                if (!role.domain || !role.name) {
                    return null;
                }

                return `${role.domain}.${role.name}`;
            })
            .filter((roleName): roleName is string => roleName !== null);

        return roleNames.length > 0 ? roleNames.join(', ') : '-';
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            groups: this.service.getUserGroups(),
            roles: this.roleService.getRoles()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ groups, roles }) => {
                    this.userGroups = groups;
                    this.allRoles = roles
                        .map((role) => ({
                            id: role.id ?? '',
                            label: `${role.domain ?? '-'}.${role.name ?? '-'}`
                        }))
                        .filter((roleOption) => roleOption.id.length > 0)
                        .sort((left, right) => left.label.localeCompare(right.label));
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

    private loadUserGroups(): void {
        this.service.getUserGroups().subscribe({
            next: (groups) => {
                this.userGroups = groups;
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

    private getEmptyUserGroup(): UserGroupEditModel {
        return {
            id: null,
            name: ''
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

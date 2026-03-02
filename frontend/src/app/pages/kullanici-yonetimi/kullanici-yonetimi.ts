import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable, of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { Table, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UserGroupRequestDto, UserGroupResponseDto } from '../../core/identity';
import { AuthService } from '../auth';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { UserRequestDto, UserResetPasswordRequestDto, UserResponseDto } from './dto';
import { KullaniciYonetimiService } from './kullanici-yonetimi.service';

interface UserGroupOption {
    label: string;
    value: string;
    roleNames: string[];
}

type ScopedCreateType = 'resepsiyonist' | 'binaYonetici' | null;

@Component({
    selector: 'app-kullanici-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, DividerModule, IconFieldModule, InputIconModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './kullanici-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class KullaniciYonetimi implements OnInit {
    private static readonly ADMIN_ROLE = 'KullaniciTipi.Admin';

    private readonly service = inject(KullaniciYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    users: UserResponseDto[] = [];
    allUserGroups: UserGroupOption[] = [];
    tesisler: TesisDto[] = [];
    selectedUser: UserResponseDto = this.getEmptyUser();
    selectedUserGroupIds: string[] = [];
    selectedTesisIdForCreate: number | null = null;
    selectedRoleNames: string[] = [];
    loading = false;
    saving = false;
    passwordSaving = false;
    dialogVisible = false;
    passwordDialogVisible = false;
    isEditMode = false;
    scopedCreateType: ScopedCreateType = null;
    selectedPasswordUserId: string | null = null;
    selectedPasswordUserName = '';
    newPassword = '';
    newPassword2 = '';
    readonly statusOptions = [
        { label: 'Standard', value: 'Standard' },
        { label: 'Must Change Password', value: 'MustChangePassword' },
        { label: 'Blocked', value: 'Blocked' }
    ];

    get canManage(): boolean {
        return this.authService.hasPermission('UserManagement.Manage');
    }

    get canAssignTesisYoneticisi(): boolean {
        return this.authService.hasPermission('KullaniciAtama.TesisYoneticisiAtayabilir');
    }

    get canAssignBinaYoneticisi(): boolean {
        return this.authService.hasPermission('KullaniciAtama.BinaYoneticisiAtayabilir');
    }

    get canAssignResepsiyonist(): boolean {
        return this.authService.hasPermission('KullaniciAtama.ResepsiyonistAtayabilir');
    }

    get isScopedTesisManager(): boolean {
        return this.canManage
            && this.authService.hasPermission('TesisYonetimi.Manage')
            && !this.authService.hasPermission('KullaniciTipi.Admin');
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
        if (!this.canManage) {
            return;
        }

        this.scopedCreateType = null;
        this.selectedUser = this.getEmptyUser();
        this.selectedUserGroupIds = [];
        this.selectedRoleNames = [];
        this.selectedTesisIdForCreate = null;
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openNewResepsiyonist(): void {
        this.openScopedCreate('resepsiyonist');
    }

    openNewBinaYoneticisi(): void {
        this.openScopedCreate('binaYonetici');
    }

    openEdit(user: UserResponseDto): void {
        this.scopedCreateType = null;
        this.selectedUser = {
            ...user,
            userGroups: [...(user.userGroups ?? [])]
        };
        this.selectedUserGroupIds = this.selectedUser.userGroups.map((group) => group.id).filter((id): id is string => !!id);
        this.selectedRoleNames = this.extractRoleNames(this.selectedUser.userGroups);
        this.selectedTesisIdForCreate = null;
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    onSelectedGroupsChange(): void {
        const selectedGroupSet = new Set(this.selectedUserGroupIds);
        const selectedGroups = this.allUserGroups.filter((groupOption) => selectedGroupSet.has(groupOption.value));
        this.selectedRoleNames = [...new Set(selectedGroups.flatMap((groupOption) => groupOption.roleNames))].sort();
    }

    saveUser(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const payload: UserRequestDto = {
            userName: this.selectedUser.userName.trim(),
            nationalId: this.selectedUser.nationalId ?? null,
            firstName: this.selectedUser.firstName ?? null,
            lastName: this.selectedUser.lastName ?? null,
            email: this.selectedUser.email ?? null,
            avatarPath: this.selectedUser.avatarPath ?? null,
            status: this.selectedUser.status,
            userGroups: this.selectedUserGroupIds.map((groupId) => ({ id: groupId } as UserGroupRequestDto))
        };

        if (!payload.userName) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Kullanici adi zorunludur.'
            });
            return;
        }

        if (!this.isEditMode && this.isScopedTesisManager && (!this.selectedTesisIdForCreate || this.selectedTesisIdForCreate <= 0)) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Tesis secimi zorunludur.'
            });
            return;
        }

        const isScopedCreate = !this.isEditMode
            && this.isScopedTesisManager
            && !!this.selectedTesisIdForCreate
            && (this.scopedCreateType === 'resepsiyonist' || this.scopedCreateType === 'binaYonetici');

        const request$: Observable<unknown> =
            this.isEditMode && this.selectedUser.id
                ? this.service.updateUser(this.selectedUser.id, payload)
                : isScopedCreate && this.scopedCreateType === 'resepsiyonist' && this.selectedTesisIdForCreate
                    ? this.service.createResepsiyonistUserForTesis(this.selectedTesisIdForCreate, payload)
                    : isScopedCreate && this.scopedCreateType === 'binaYonetici' && this.selectedTesisIdForCreate
                        ? this.service.createBinaYoneticisiUserForTesis(this.selectedTesisIdForCreate, payload)
                    : this.service.createUser(payload);

        this.saving = true;
        request$
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
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Kullanici guncellendi.' : 'Kullanici olusturuldu.'
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

    get showScopedCreateButtons(): boolean {
        return this.canManage && this.isScopedTesisManager;
    }

    get showTesisSelectorForCreate(): boolean {
        return !this.isEditMode && this.isScopedTesisManager;
    }

    deleteUser(user: UserResponseDto): void {
        if (!this.canManage || !user.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${user.userName}" kullanicisini silmek istediginize emin misiniz?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteUser(user.id!).subscribe({
                    next: () => {
                        this.loadData();
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Basarili',
                            detail: 'Kullanici silindi.'
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

    openResetPassword(user: UserResponseDto): void {
        if (!this.canManage || !user.id) {
            return;
        }

        this.selectedPasswordUserId = user.id;
        this.selectedPasswordUserName = user.userName;
        this.newPassword = '';
        this.newPassword2 = '';
        this.passwordDialogVisible = true;
    }

    resetPassword(): void {
        if (!this.canManage || this.passwordSaving || !this.selectedPasswordUserId) {
            return;
        }

        if (this.newPassword.trim().length === 0 || this.newPassword2.trim().length === 0) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Yeni parola ve tekrari zorunludur.'
            });
            return;
        }

        if (this.newPassword !== this.newPassword2) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Parola Uyusmazligi',
                detail: 'Yeni parola ve tekrari ayni olmalidir.'
            });
            return;
        }

        const payload: UserResetPasswordRequestDto = {
            newPassword: this.newPassword,
            newPassword2: this.newPassword2
        };

        this.passwordSaving = true;
        this.service
            .resetUserPassword(this.selectedPasswordUserId, payload)
            .pipe(
                finalize(() => {
                    this.passwordSaving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.passwordDialogVisible = false;
                    this.selectedPasswordUserId = null;
                    this.selectedPasswordUserName = '';
                    this.newPassword = '';
                    this.newPassword2 = '';
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: 'Kullanici parolasi degistirildi. Kullanici ilk giriste parolasini degistirecek.'
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

    getUserFullName(user: UserResponseDto): string {
        const fullName = `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim();
        return fullName.length > 0 ? fullName : '-';
    }

    getUserGroupsText(user: UserResponseDto): string {
        const groups = user.userGroups ?? [];
        if (groups.length === 0) {
            return '-';
        }

        return groups.map((group) => group.name).join(', ');
    }

    getStatusSeverity(status: string): 'success' | 'warn' | 'danger' | 'info' {
        const normalizedStatus = status.trim().toLowerCase();
        if (normalizedStatus.includes('active') || normalizedStatus.includes('aktif')) {
            return 'success';
        }

        if (normalizedStatus.includes('passive') || normalizedStatus.includes('pasif')) {
            return 'danger';
        }

        if (normalizedStatus.includes('change')) {
            return 'warn';
        }

        if (normalizedStatus.includes('standard')) {
            return 'success';
        }

        if (normalizedStatus.includes('mustchangepassword')) {
            return 'warn';
        }

        if (normalizedStatus.includes('blocked')) {
            return 'danger';
        }

        return 'info';
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            users: this.service.getUsers(),
            userGroups: this.service.getUserGroups(),
            tesisler: this.isScopedTesisManager ? this.service.getTesisler() : of([])
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ users, userGroups, tesisler }) => {
                    this.users = users;
                    this.allUserGroups = userGroups
                        .map((userGroup) => this.mapToUserGroupOption(userGroup))
                        .filter((groupOption) => this.canAssignGroup(groupOption));
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
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

    private mapToUserGroupOption(userGroup: UserGroupResponseDto): UserGroupOption {
        return {
            label: userGroup.name,
            value: userGroup.id ?? '',
            roleNames: this.extractRoleNames([userGroup])
        };
    }

    private canAssignGroup(group: UserGroupOption): boolean {
        if (!this.isScopedTesisManager) {
            return true;
        }

        if (this.isAdminGroup(group.roleNames)) {
            return false;
        }

        const isTesisYoneticisiGroup = this.isTesisYoneticisiGroup(group.roleNames);
        const isBinaYoneticisiGroup = this.isBinaYoneticisiGroup(group.roleNames);
        const isResepsiyonistGroup = this.isResepsiyonistGroup(group.roleNames);

        if (isTesisYoneticisiGroup && !this.canAssignTesisYoneticisi) {
            return false;
        }

        if (isBinaYoneticisiGroup && !this.canAssignBinaYoneticisi) {
            return false;
        }

        if (isResepsiyonistGroup && !this.canAssignResepsiyonist) {
            return false;
        }

        return isTesisYoneticisiGroup || isBinaYoneticisiGroup || isResepsiyonistGroup;
    }

    private openScopedCreate(type: Exclude<ScopedCreateType, null>): void {
        if (!this.canManage || !this.isScopedTesisManager) {
            return;
        }

        const markerRole = type === 'resepsiyonist'
            ? 'KullaniciAtama.ResepsiyonistAtanabilir'
            : 'KullaniciAtama.BinaYoneticisiAtanabilir';

        const groupId = this.findGroupIdByMarkerRole(markerRole);
        if (!groupId) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Grup Bulunamadi',
                detail: type === 'resepsiyonist'
                    ? 'Resepsiyonist grubuna ait atanabilir kayit bulunamadi.'
                    : 'Bina yoneticisi grubuna ait atanabilir kayit bulunamadi.'
            });
            return;
        }

        this.scopedCreateType = type;
        this.selectedUser = this.getEmptyUser();
        this.selectedUserGroupIds = [groupId];
        this.selectedRoleNames = this.extractSelectedRoleNames(this.selectedUserGroupIds);
        this.selectedTesisIdForCreate = null;
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    private findGroupIdByMarkerRole(markerRole: string): string | null {
        const matchedGroup = this.allUserGroups.find(
            (group) => group.roleNames.includes(markerRole) && !this.isAdminGroup(group.roleNames)
        );
        return matchedGroup?.value ?? null;
    }

    private extractSelectedRoleNames(selectedGroupIds: string[]): string[] {
        const selectedGroupSet = new Set(selectedGroupIds);
        return [
            ...new Set(
                this.allUserGroups
                    .filter((groupOption) => selectedGroupSet.has(groupOption.value))
                    .flatMap((groupOption) => groupOption.roleNames)
            )
        ].sort();
    }

    private isTesisYoneticisiGroup(roleNames: string[]): boolean {
        return roleNames.includes('KullaniciAtama.TesisYoneticisiAtanabilir');
    }

    private isBinaYoneticisiGroup(roleNames: string[]): boolean {
        return roleNames.includes('KullaniciAtama.BinaYoneticisiAtanabilir');
    }

    private isResepsiyonistGroup(roleNames: string[]): boolean {
        return roleNames.includes('KullaniciAtama.ResepsiyonistAtanabilir');
    }

    private isAdminGroup(roleNames: string[]): boolean {
        return roleNames.includes(KullaniciYonetimi.ADMIN_ROLE);
    }

    private extractRoleNames(groups: UserGroupResponseDto[] | null | undefined): string[] {
        if (!groups || groups.length === 0) {
            return [];
        }

        return [
            ...new Set(
                groups.flatMap((group) =>
                    (group.roles ?? [])
                        .map((role) => {
                            if (!role.domain || !role.name) {
                                return null;
                            }

                            return `${role.domain}.${role.name}`;
                        })
                        .filter((roleName): roleName is string => roleName !== null)
                )
            )
        ].sort();
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

    private getEmptyUser(): UserResponseDto {
        return {
            userName: '',
            firstName: '',
            lastName: '',
            email: '',
            status: 'MustChangePassword',
            userGroups: []
        };
    }
}

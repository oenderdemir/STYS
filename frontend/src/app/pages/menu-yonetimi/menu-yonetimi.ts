import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin, Observable } from 'rxjs';
import { ConfirmationService, MessageService, TreeNode } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TreeTableModule } from 'primeng/treetable';
import { tryReadApiMessage } from '../../core/api';
import { RoleResponseDto } from '../../core/identity';
import { MenuItemDto, MenuRuntimeService } from '../../core/menu';
import { AuthService } from '../auth';
import { RolYonetimiService } from '../rol-yonetimi/rol-yonetimi.service';
import { MenuItemRequestDto } from './dto';
import { MenuYonetimiService } from './menu-yonetimi.service';

interface RoleOption {
    id: string;
    label: string;
}

interface ParentMenuOption {
    id: string;
    label: string;
}

interface MenuItemEditModel {
    id?: string | null;
    label: string;
    icon: string;
    route: string;
    queryParams: string;
    parentId?: string | null;
    menuOrder: number;
}

@Component({
    selector: 'app-menu-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, DividerModule, InputTextModule, MultiSelectModule, SelectModule, ToastModule, ToolbarModule, TreeTableModule],
    templateUrl: './menu-yonetimi.html',
    providers: [MessageService, ConfirmationService]
})
export class MenuYonetimi implements OnInit {
    private readonly service = inject(MenuYonetimiService);
    private readonly roleService = inject(RolYonetimiService);
    private readonly menuRuntimeService = inject(MenuRuntimeService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    menuItems: MenuItemDto[] = [];
    treeMenuItems: TreeNode<MenuItemDto>[] = [];
    parentMenuOptions: ParentMenuOption[] = [{ id: '', label: '/' }];
    allRoles: RoleOption[] = [];

    selectedMenuItem: MenuItemEditModel = this.getEmptyMenuItem();
    selectedParentId = '';
    selectedRoleIds: string[] = [];
    dialogVisible = false;
    isEditMode = false;
    loading = false;
    saving = false;

    get canManage(): boolean {
        return this.authService.hasPermission('MenuManagement.Manage');
    }

    ngOnInit(): void {
        this.loadData();
    }

    refresh(): void {
        this.loadData();
    }

    openNew(): void {
        if (!this.canManage) {
            return;
        }

        this.selectedMenuItem = this.getEmptyMenuItem();
        this.selectedParentId = '';
        this.selectedRoleIds = [];
        this.parentMenuOptions = this.buildParentMenuOptions();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openNewChild(parentId: string | null | undefined): void {
        if (!this.canManage) {
            return;
        }

        if (!parentId) {
            this.openNew();
            return;
        }

        this.selectedMenuItem = this.getEmptyMenuItem();
        this.selectedParentId = parentId;
        this.selectedRoleIds = [];
        this.parentMenuOptions = this.buildParentMenuOptions();
        this.isEditMode = false;
        this.dialogVisible = true;
    }

    openEdit(item: MenuItemDto): void {
        this.selectedMenuItem = {
            id: item.id ?? null,
            label: item.label ?? '',
            icon: item.icon ?? '',
            route: item.route ?? '',
            queryParams: item.queryParams ?? '',
            parentId: item.parentId ?? null,
            menuOrder: item.menuOrder ?? 0
        };
        this.selectedParentId = item.parentId ?? '';
        this.selectedRoleIds = (item.roles ?? []).map((role) => role.id).filter((roleId): roleId is string => !!roleId);
        this.parentMenuOptions = this.buildParentMenuOptions(item.id ?? undefined);
        this.isEditMode = true;
        this.dialogVisible = true;
    }

    saveMenuItem(): void {
        if (!this.canManage || this.saving) {
            return;
        }

        const label = this.selectedMenuItem.label.trim();
        if (!label) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Menu label zorunludur.'
            });
            return;
        }

        const payload: MenuItemRequestDto = {
            id: this.selectedMenuItem.id ?? null,
            label,
            icon: this.selectedMenuItem.icon.trim() || null,
            route: this.selectedMenuItem.route.trim() || null,
            queryParams: this.selectedMenuItem.queryParams.trim() || null,
            parentId: this.selectedParentId || null,
            menuOrder: this.selectedMenuItem.menuOrder ?? 0,
            roles: this.selectedRoleIds.map((roleId) => ({ id: roleId } as RoleResponseDto)),
            items: []
        };

        this.saving = true;
        const save$: Observable<unknown> =
            this.isEditMode && this.selectedMenuItem.id ? this.service.updateMenuItem(this.selectedMenuItem.id, payload) : this.service.createMenuItem(payload);

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
                    this.loadMenuItems();
                    this.menuRuntimeService.refreshMenu();
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.isEditMode ? 'Menu guncellendi.' : 'Menu eklendi.'
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

    deleteMenuItem(item: MenuItemDto): void {
        if (!this.canManage || !item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${item.label ?? '-'}" menusu silinsin mi?`,
            header: 'Silme Onayi',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonStyleClass: 'p-button-danger',
            rejectButtonStyleClass: 'p-button-secondary',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.deleteMenuItem(item.id!).subscribe({
                    next: () => {
                        this.loadMenuItems();
                        this.menuRuntimeService.refreshMenu();
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Basarili',
                            detail: 'Menu silindi.'
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

    hasChild(rowNode: TreeNode<MenuItemDto>): boolean {
        return !!rowNode.children && rowNode.children.length > 0;
    }

    private loadData(): void {
        this.loading = true;
        forkJoin({
            menuItems: this.service.getMenuItems(),
            roles: this.roleService.getViewRoles()
        })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ menuItems, roles }) => {
                    this.menuItems = [...menuItems].sort((left, right) => (left.menuOrder ?? 0) - (right.menuOrder ?? 0));
                    this.treeMenuItems = this.buildTree(this.menuItems);
                    this.parentMenuOptions = this.buildParentMenuOptions(this.selectedMenuItem.id ?? undefined);
                    this.allRoles = roles
                        .filter((role) => (role.name ?? '').toLowerCase() === 'menu')
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

    private loadMenuItems(): void {
        this.service.getMenuItems().subscribe({
            next: (menuItems) => {
                this.menuItems = [...menuItems].sort((left, right) => (left.menuOrder ?? 0) - (right.menuOrder ?? 0));
                this.treeMenuItems = this.buildTree(this.menuItems);
                this.parentMenuOptions = this.buildParentMenuOptions(this.selectedMenuItem.id ?? undefined);
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

    private buildTree(items: MenuItemDto[]): TreeNode<MenuItemDto>[] {
        const map = new Map<string, TreeNode<MenuItemDto>>();

        for (const item of items) {
            if (!item.id) {
                continue;
            }

            map.set(item.id, {
                key: item.id,
                data: item,
                children: []
            });
        }

        const roots: TreeNode<MenuItemDto>[] = [];
        for (const item of items) {
            if (!item.id) {
                continue;
            }

            const currentNode = map.get(item.id);
            if (!currentNode) {
                continue;
            }

            if (item.parentId) {
                const parentNode = map.get(item.parentId);
                if (parentNode) {
                    parentNode.children?.push(currentNode);
                    continue;
                }
            }

            roots.push(currentNode);
        }

        return roots;
    }

    private buildParentMenuOptions(excludedId?: string): ParentMenuOption[] {
        return [
            { id: '', label: '/' },
            ...this.menuItems
                .filter((item) => item.id && item.id !== excludedId)
                .map((item) => ({
                    id: item.id!,
                    label: item.label ?? '-'
                }))
        ];
    }

    private getEmptyMenuItem(): MenuItemEditModel {
        return {
            id: null,
            label: '',
            icon: '',
            route: '',
            queryParams: '',
            parentId: null,
            menuOrder: 0
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

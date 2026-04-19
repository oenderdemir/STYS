import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../../pages/auth';
import { MenuItemDto, MenuRoleDto } from './dto';
import { AppMenuItem } from './models';
import { MenuApiService } from './menu-api.service';


@Injectable({ providedIn: 'root' })
export class MenuRuntimeService {
    private readonly authService = inject(AuthService);
    private readonly menuApiService = inject(MenuApiService);

    private readonly loadedForToken = signal<string | null>(null);
    private readonly rawMenuItems = signal<AppMenuItem[]>([]);
    private readonly userPermissions = signal<string[]>([]);

    readonly menuItems = computed(() => {
        const permissionSet = new Set(this.userPermissions().map((permission) => this.normalizePermission(permission)));
        return this.filterMenuItems(this.rawMenuItems(), permissionSet);
    });

    constructor() {
        effect(() => {
            this.authService.sessionRevision();

            if (!this.authService.isAuthenticated()) {
                this.clearMenu();
                return;
            }

            this.userPermissions.set(this.authService.getUserPermissions());
            this.ensureMenuLoaded();
        });
    }

    ensureMenuLoaded(): void {
        const token = this.authService.getToken();
        if (!token) {
            this.clearMenu();
            return;
        }

        if (this.loadedForToken() === token) {
            return;
        }

        this.fetchMenuTree();
    }

    refreshMenu(): void {
        this.loadedForToken.set(null);
        this.ensureMenuLoaded();
    }

    clearMenu(): void {
        this.userPermissions.set([]);
        this.rawMenuItems.set([]);
        this.loadedForToken.set(null);
    }

    private fetchMenuTree(): void {
        this.menuApiService
            .getMenuTree()
            .pipe(
                map((menuTree) => this.mapMenuItems(menuTree)),
                catchError(() => of(null))
            )
            .subscribe((menuItems) => {
                if (menuItems === null) {
                    return;
                }

                this.rawMenuItems.set(menuItems);
                this.loadedForToken.set(this.authService.getToken());
            });
    }

    private mapMenuItems(menuTree: MenuItemDto[] | null | undefined): AppMenuItem[] {
        if (!menuTree || menuTree.length === 0) {
            return [];
        }

        const mapped = [...menuTree]
            .sort((left, right) => (left.menuOrder ?? 0) - (right.menuOrder ?? 0))
            .map((menuItemDto) => this.mapMenuItem(menuItemDto, '/menu'))
            .filter((item): item is AppMenuItem => item !== null);

        return mapped;
    }

    private mapMenuItem(menuItemDto: MenuItemDto, parentPath: string): AppMenuItem | null {
        const itemKey = this.createPathKey(menuItemDto);
        const itemPath = `${parentPath}/${itemKey}`;

        const children = this.mapMenuItems(menuItemDto.items);
        const route = this.normalizeRoute(menuItemDto.route);

        if (!route && children.length === 0) {
            return null;
        }

        const mappedItem: AppMenuItem = {
            label: menuItemDto.label ?? '',
            icon: menuItemDto.icon ?? undefined,
            path: itemPath,
            roles: this.toPermissionList(menuItemDto.roles),
            items: children.length > 0 ? children : undefined
        };

        if (route?.startsWith('http://') || route?.startsWith('https://')) {
            mappedItem.url = route;
            mappedItem.target = '_blank';
            return mappedItem;
        }

        if (route) {
            mappedItem.routerLink = [route];
            mappedItem.queryParams = this.parseQueryParams(menuItemDto.queryParams);
        }

        return mappedItem;
    }

    private filterMenuItems(menuItems: AppMenuItem[], permissionSet: Set<string>): AppMenuItem[] {
        return menuItems.reduce<AppMenuItem[]>((accumulator, menuItem) => {
            const filteredChildren = menuItem.items ? this.filterMenuItems(menuItem.items, permissionSet) : undefined;
            const hasOwnPermission = this.hasPermission(menuItem.roles, permissionSet);
            const hasChildren = !!filteredChildren && filteredChildren.length > 0;
            const hasAction = !!menuItem.routerLink || !!menuItem.url || !!menuItem.command;

            if (!hasOwnPermission && !hasChildren) {
                return accumulator;
            }

            if (hasOwnPermission && !hasChildren && !hasAction) {
                return accumulator;
            }

            accumulator.push({
                ...menuItem,
                routerLink: hasOwnPermission ? menuItem.routerLink : undefined,
                url: hasOwnPermission ? menuItem.url : undefined,
                command: hasOwnPermission ? menuItem.command : undefined,
                queryParams: hasOwnPermission ? menuItem.queryParams : undefined,
                items: hasChildren ? filteredChildren : undefined
            });

            return accumulator;
        }, []);
    }

    private toPermissionList(roles: MenuRoleDto[] | null | undefined): string[] {
        if (!roles || roles.length === 0) {
            return [];
        }

        return roles
            .map((role) => {
                if (!role.domain || !role.name) {
                    return null;
                }

                return `${role.domain}.${role.name}`;
            })
            .filter((permission): permission is string => permission !== null);
    }

    private hasPermission(requiredPermissions: string[] | null | undefined, permissionSet: Set<string>): boolean {
        if (!requiredPermissions || requiredPermissions.length === 0) {
            return true;
        }

        return requiredPermissions.some((permission) => permissionSet.has(this.normalizePermission(permission)));
    }

    private normalizeRoute(route: string | null | undefined): string | null {
        if (!route || route.trim().length === 0) {
            return null;
        }

        const normalizedRoute = route.trim();
        if (normalizedRoute.startsWith('http://') || normalizedRoute.startsWith('https://')) {
            return normalizedRoute;
        }

        if (normalizedRoute.startsWith('/')) {
            return normalizedRoute;
        }

        return `/${normalizedRoute}`;
    }

    private parseQueryParams(rawQueryParams: string | null | undefined): Record<string, unknown> | undefined {
        if (!rawQueryParams || rawQueryParams.trim().length === 0) {
            return undefined;
        }

        const trimmed = rawQueryParams.trim();
        if (trimmed.startsWith('{')) {
            try {
                const parsed = JSON.parse(trimmed);
                if (typeof parsed === 'object' && parsed !== null) {
                    return parsed as Record<string, unknown>;
                }
            } catch {
                return undefined;
            }
        }

        const searchParams = new URLSearchParams(trimmed.startsWith('?') ? trimmed.slice(1) : trimmed);
        const params: Record<string, unknown> = {};
        for (const [key, value] of searchParams.entries()) {
            params[key] = value;
        }

        return Object.keys(params).length > 0 ? params : undefined;
    }

    private createPathKey(menuItemDto: MenuItemDto): string {
        const key = menuItemDto.id ?? menuItemDto.label ?? 'item';
        const normalized = key
            .toString()
            .trim()
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '');

        return normalized.length > 0 ? normalized : 'item';
    }

    private normalizePermission(permission: string): string {
        return permission.trim().toLowerCase();
    }

}



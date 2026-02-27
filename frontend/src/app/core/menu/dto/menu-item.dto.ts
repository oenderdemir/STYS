import { MenuRoleDto } from './menu-role.dto';

export interface MenuItemDto {
    id?: string | null;
    label?: string | null;
    icon?: string | null;
    route?: string | null;
    queryParams?: string | null;
    parentId?: string | null;
    menuOrder?: number;
    roles?: MenuRoleDto[] | null;
    items?: MenuItemDto[] | null;
}

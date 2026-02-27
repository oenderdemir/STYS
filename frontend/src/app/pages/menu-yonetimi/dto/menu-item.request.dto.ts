import { RoleResponseDto } from '../../../core/identity';

export interface MenuItemRequestDto {
    id?: string | null;
    label?: string | null;
    icon?: string | null;
    route?: string | null;
    queryParams?: string | null;
    parentId?: string | null;
    menuOrder: number;
    roles?: RoleResponseDto[] | null;
    items?: MenuItemRequestDto[] | null;
}

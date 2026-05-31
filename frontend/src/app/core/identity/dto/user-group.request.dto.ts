import { RoleResponseDto } from './role.response.dto';

export interface UserGroupRequestDto {
    id?: string | null;
    name: string;
    defaultRoute?: string | null;
    roles?: RoleResponseDto[] | null;
}

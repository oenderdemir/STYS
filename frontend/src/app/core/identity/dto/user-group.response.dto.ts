import { RoleResponseDto } from './role.response.dto';

export interface UserGroupResponseDto {
    id?: string | null;
    name: string;
    roles?: RoleResponseDto[] | null;
}

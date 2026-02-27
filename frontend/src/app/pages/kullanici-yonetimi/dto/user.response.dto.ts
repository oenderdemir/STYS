import { UserGroupResponseDto } from '../../../core/identity';

export interface UserResponseDto {
    id?: string | null;
    userName: string;
    nationalId?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    email?: string | null;
    avatarPath?: string | null;
    status: string;
    userGroups: UserGroupResponseDto[];
}

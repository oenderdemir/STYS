import { UserGroupRequestDto } from '../../../core/identity';

export interface UserRequestDto {
    userName: string;
    nationalId?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    email?: string | null;
    avatarPath?: string | null;
    status: string;
    userGroups: UserGroupRequestDto[];
}

import { MenuItem } from 'primeng/api';

export interface AppMenuItem extends MenuItem {
    roles?: string[];
    items?: AppMenuItem[];
}

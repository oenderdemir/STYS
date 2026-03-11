export const NotificationSeverityValues = {
    Success: 'success',
    Info: 'info',
    Warn: 'warn',
    Error: 'error',
    Danger: 'danger'
} as const;

export type NotificationSeverity = (typeof NotificationSeverityValues)[keyof typeof NotificationSeverityValues];

export interface NotificationDto {
    id: number;
    tip: string;
    baslik: string;
    mesaj: string;
    link?: string | null;
    severity: NotificationSeverity | string;
    isRead: boolean;
    createdAt: string;
}

export interface NotificationViewModel {
    id: number;
    tip: string;
    baslik: string;
    mesaj: string;
    link?: string | null;
    severity: NotificationSeverity | string;
    isRead: boolean;
    createdAt: Date;
}

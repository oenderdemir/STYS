import { NotificationSeverity } from './notification.model';

export interface NotificationPreferenceDto {
    bildirimlerAktifMi: boolean;
    minimumSeverity: NotificationSeverity | string;
    izinliTipler: string[];
    izinliKaynaklar: string[];
    mevcutTipler: string[];
    mevcutKaynaklar: string[];
}

export interface UpdateNotificationPreferenceRequestDto {
    bildirimlerAktifMi: boolean;
    minimumSeverity: NotificationSeverity | string;
    izinliTipler: string[];
    izinliKaynaklar: string[];
}

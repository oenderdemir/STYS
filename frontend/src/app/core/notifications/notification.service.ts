import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { firstValueFrom, map } from 'rxjs';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getApiBaseUrl } from '../config';
import { ApiResponse, tryReadApiMessage } from '../api';
import { AuthService } from '../../pages/auth';
import { NotificationDto, NotificationViewModel } from './notification.model';

@Injectable({ providedIn: 'root' })
export class NotificationService {
    private readonly http = inject(HttpClient);
    private readonly authService = inject(AuthService);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly hubUrl = `${this.apiBaseUrl}/ui/bildirim-hub`;
    private hubConnection: HubConnection | null = null;
    private connectionToken: string | null = null;

    readonly notifications = signal<NotificationViewModel[]>([]);
    readonly unreadCount = signal(0);
    readonly isConnected = signal(false);
    readonly lastRealtimeNotification = signal<NotificationViewModel | null>(null);

    constructor() {
        effect(() => {
            this.authService.sessionRevision();
            if (!this.authService.isAuthenticated()) {
                this.resetState();
                return;
            }

            void this.reload();
            void this.ensureHubConnection();
        });
    }

    async reload(): Promise<void> {
        if (!this.authService.isAuthenticated()) {
            this.resetState();
            return;
        }

        try {
            const [list, unreadCount] = await Promise.all([
                firstValueFrom(
                    this.http.get<ApiResponse<NotificationDto[]>>(`${this.apiBaseUrl}/ui/bildirim?take=20`).pipe(
                        map((responseEnvelope) => {
                            if (responseEnvelope.success && responseEnvelope.data) {
                                return responseEnvelope.data;
                            }

                            throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bildirim listesi alinamadi.');
                        })
                    )
                ),
                firstValueFrom(
                    this.http.get<ApiResponse<number>>(`${this.apiBaseUrl}/ui/bildirim/unread-count`).pipe(
                        map((responseEnvelope) => {
                            if (responseEnvelope.success && responseEnvelope.data !== null) {
                                return responseEnvelope.data;
                            }

                            throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Okunmamis bildirim sayisi alinamadi.');
                        })
                    )
                )
            ]);

            this.notifications.set(list.map((x) => this.mapToViewModel(x)));
            this.unreadCount.set(unreadCount);
        } catch {
            // Bildirim islemi yardimci bir akis oldugu icin UI'yi bloklamiyoruz.
        }
    }

    markAsRead(notificationId: number) {
        return this.http.post<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/bildirim/${notificationId}/read`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Bildirim okundu olarak isaretlenemedi.');
            })
        );
    }

    markAllAsRead() {
        return this.http.post<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/bildirim/read-all`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tum bildirimler okundu olarak isaretlenemedi.');
            })
        );
    }

    applyRead(notificationId: number): void {
        let wasUnread = false;
        this.notifications.update((items) =>
            items.map((item) => {
                if (item.id !== notificationId || item.isRead) {
                    return item;
                }

                wasUnread = true;
                return { ...item, isRead: true };
            })
        );

        if (wasUnread) {
            this.unreadCount.update((value) => Math.max(0, value - 1));
        }
    }

    applyReadAll(): void {
        const unread = this.notifications().filter((x) => !x.isRead).length;
        if (unread <= 0) {
            return;
        }

        this.notifications.update((items) => items.map((item) => (item.isRead ? item : { ...item, isRead: true })));
        this.unreadCount.set(0);
    }

    private async ensureHubConnection(): Promise<void> {
        const token = this.authService.getToken();
        if (!token) {
            await this.stopHubConnection();
            return;
        }

        if (this.hubConnection && this.connectionToken === token) {
            if (
                this.hubConnection.state === HubConnectionState.Connected ||
                this.hubConnection.state === HubConnectionState.Connecting ||
                this.hubConnection.state === HubConnectionState.Reconnecting
            ) {
                return;
            }
        }

        await this.stopHubConnection();

        const connection = new HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                accessTokenFactory: () => this.authService.getToken() ?? ''
            })
            .withAutomaticReconnect()
            .build();

        connection.on('bildirim-alindi', (payload: NotificationDto) => {
            const notification = this.mapToViewModel(payload);
            this.notifications.update((items) => {
                const withoutExisting = items.filter((x) => x.id !== notification.id);
                return [notification, ...withoutExisting].slice(0, 20);
            });
            this.lastRealtimeNotification.set(notification);

            if (!notification.isRead) {
                this.unreadCount.update((value) => value + 1);
            }
        });

        connection.onclose(() => {
            if (this.hubConnection === connection) {
                this.isConnected.set(false);
            }
        });

        this.hubConnection = connection;
        this.connectionToken = token;

        try {
            await connection.start();
            this.isConnected.set(true);
        } catch {
            this.isConnected.set(false);
        }
    }

    private async stopHubConnection(): Promise<void> {
        if (!this.hubConnection) {
            this.connectionToken = null;
            this.isConnected.set(false);
            return;
        }

        try {
            await this.hubConnection.stop();
        } catch {
            // No-op
        } finally {
            this.hubConnection = null;
            this.connectionToken = null;
            this.isConnected.set(false);
        }
    }

    private resetState(): void {
        this.notifications.set([]);
        this.unreadCount.set(0);
        this.lastRealtimeNotification.set(null);
        void this.stopHubConnection();
    }

    private mapToViewModel(dto: NotificationDto): NotificationViewModel {
        const parsedDate = new Date(dto.createdAt);
        return {
            id: dto.id,
            tip: dto.tip,
            baslik: dto.baslik,
            mesaj: dto.mesaj,
            link: dto.link,
            severity: dto.severity,
            isRead: dto.isRead,
            createdAt: Number.isNaN(parsedDate.getTime()) ? new Date() : parsedDate
        };
    }
}

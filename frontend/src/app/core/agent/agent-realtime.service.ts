import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { Injectable, Signal, signal, effect, inject, DestroyRef } from '@angular/core';
import { AuthService } from '../../pages/auth/auth.service';
import { getApiBaseUrl } from '../config';
import { AgentCommandDto } from '../../pages/agent-yonetimi/agent-yonetimi.dto';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AgentRealtimeService {
    private readonly authService = inject(AuthService);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly hubUrl = `${this.apiBaseUrl}/ui/agent-hub`;
    private hubConnection: HubConnection | null = null;
    private connectionToken: string | null = null;
    private currentAgentId: number | null = null;
    private readonly destroyRef = inject(DestroyRef);

    readonly commandUpdates = signal<AgentCommandDto | null>(null);

    constructor() {
        effect(() => {
            this.authService.sessionRevision();
            if (!this.authService.isAuthenticated()) {
                this.stopConnection();
                return;
            }
            if (this.currentAgentId !== null) {
                void this.ensureConnection();
            }
        });
    }

    joinAgentGroup(agentId: number): void {
        if (this.currentAgentId === agentId) return;
        if (this.currentAgentId !== null) {
            void this.invokeLeaveGroup(this.currentAgentId);
        }
        this.currentAgentId = agentId;
        void this.ensureConnection();
        void this.invokeJoinGroup(agentId);
    }

    leaveAgentGroup(): void {
        if (this.currentAgentId !== null) {
            void this.invokeLeaveGroup(this.currentAgentId);
            this.currentAgentId = null;
        }
    }

    private async ensureConnection(): Promise<void> {
        const token = this.authService.getToken();
        if (!token) { this.stopConnection(); return; }

        if (this.hubConnection && this.connectionToken === token) {
            if (this.hubConnection.state === HubConnectionState.Connected ||
                this.hubConnection.state === HubConnectionState.Connecting ||
                this.hubConnection.state === HubConnectionState.Reconnecting) {
                return;
            }
        }

        this.stopConnection();

        const connection = new HubConnectionBuilder()
            .withUrl(this.hubUrl, {
                accessTokenFactory: () => this.authService.getToken() ?? '',
                withCredentials: false
            })
            .withAutomaticReconnect()
            .build();

        connection.on('AgentCommandUpdated', (payload: AgentCommandDto) => {
            this.commandUpdates.set(payload);
        });

        connection.onclose(() => {
            if (this.hubConnection === connection) {
                this.hubConnection = null;
            }
        });

        this.hubConnection = connection;
        this.connectionToken = token;

        try {
            await connection.start();
            if (this.currentAgentId !== null) {
                await this.invokeJoinGroup(this.currentAgentId);
            }
        } catch {
            this.hubConnection = null;
        }
    }

    private async invokeJoinGroup(agentId: number): Promise<void> {
        try {
            await this.hubConnection?.invoke('JoinAgentGroupAsync', agentId);
        } catch (err) {
            console.warn('[AgentRealtime] JoinAgentGroup failed:', err);
        }
    }

    private async invokeLeaveGroup(agentId: number): Promise<void> {
        try {
            await this.hubConnection?.invoke('LeaveAgentGroupAsync', agentId);
        } catch (err) {
            console.warn('[AgentRealtime] LeaveAgentGroup failed:', err);
        }
    }

    private stopConnection(): void {
        if (!this.hubConnection) return;
        try { void this.hubConnection.stop(); }
        catch { }
        this.hubConnection = null;
        this.connectionToken = null;
    }
}

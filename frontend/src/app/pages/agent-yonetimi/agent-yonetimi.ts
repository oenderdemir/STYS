import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, inject, signal, effect } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabViewModule } from 'primeng/tabview';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { CheckboxModule } from 'primeng/checkbox';
import { DropdownModule } from 'primeng/dropdown';
import { AgentRealtimeService } from '../../core/agent/agent-realtime.service';
import {
    AgentDto,
    AgentDurumLabels,
    AgentEnrollmentCodeDto,
    AgentEnrollmentCodeRequest,
    AgentListDto,
    AgentKaydetRequest
} from './agent-yonetimi.dto';
import { AgentYonetimiService } from './agent-yonetimi.service';
import { KurumService } from '../kurum-yonetimi/kurum.service';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';

type AgentFormState = AgentKaydetRequest & { id?: number };

@Component({
    selector: 'app-agent-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DialogModule,
        InputTextModule,
        MultiSelectModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        CheckboxModule,
        TabViewModule,
        DropdownModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './agent-yonetimi.html'
})
export class AgentYonetimiComponent implements OnInit, OnDestroy {
    private readonly service = inject(AgentYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly realtime = inject(AgentRealtimeService);

    agents = signal<AgentListDto[]>([]);
    loading = signal(false);
    dialogVisible = signal(false);
    enrollmentDialogVisible = signal(false);
    enrollmentCodes = signal<AgentEnrollmentCodeDto[]>([]);
    submitted = signal(false);
    commands = signal<AgentCommandDto[]>([]);
    commandsLoading = signal(false);
    selectedCommandType = signal<string>('Ping');
    viewingAgentId = signal<number | null>(null);

    agentForm: AgentFormState = { ad: '', kurumId: 0, tesisIds: [], scopes: [] };
    enrollmentForm: AgentEnrollmentCodeRequest = { kurumId: 0, tesisIds: [], allowedScopes: [] };

    durumLabels = AgentDurumLabels;
    commandTypes = ['Ping', 'HealthCheck', 'RefreshConfiguration', 'PavoConnectionTest'];

    constructor() {
        effect(() => {
            const update = this.realtime.commandUpdates();
            if (update && this.viewingAgentId() && update.agentId === this.viewingAgentId()) {
                this.commands.update(list => {
                    const without = list.filter(c => c.id !== update.id);
                    return [update, ...without];
                });
            }
        });
    }

    ngOnInit(): void {
        this.loadAgents();
    }

    ngOnDestroy(): void {
        this.realtime.leaveAgentGroup();
    }

    loadAgents(): void {
        this.loading.set(true);
        this.service.getAgents().pipe(finalize(() => this.loading.set(false))).subscribe({
            next: (data) => this.agents.set(data),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    openNew(): void {
        this.agentForm = { ad: '', kurumId: 0, tesisIds: [], scopes: [] };
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    editAgent(agent: AgentListDto): void {
        this.service.getAgent(agent.id).subscribe({
            next: (detail) => {
                this.agentForm = {
                    id: detail.id, ad: detail.ad, kurumId: detail.kurumId,
                    tesisIds: detail.tesisIds, scopes: detail.scopes
                };
                this.submitted.set(false);
                this.dialogVisible.set(true);
                this.viewingAgentId.set(agent.id);
                this.realtime.joinAgentGroup(agent.id);
                this.loadCommands(agent.id);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    saveAgent(): void {
        this.submitted.set(true);
        if (!this.agentForm.ad || !this.agentForm.kurumId) return;

        const request: AgentKaydetRequest = {
            ad: this.agentForm.ad,
            kurumId: this.agentForm.kurumId,
            tesisIds: this.agentForm.tesisIds,
            scopes: this.agentForm.scopes
        };

        const action = this.agentForm.id
            ? this.service.updateAgent(this.agentForm.id, request)
            : this.service.createAgent(request);

        action.subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Agent kaydedildi.' });
                this.dialogVisible.set(false);
                this.loadAgents();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    deleteAgent(agent: AgentListDto): void {
        this.confirmationService.confirm({
            message: `${agent.ad} agent'ını silmek istediğinize emin misiniz?`,
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.disableAgent(agent.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Agent devre dışı bırakıldı.' });
                        this.loadAgents();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    approveAgent(agent: AgentListDto): void {
        this.service.approveAgent(agent.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Agent onaylandı.' });
                this.loadAgents();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    revokeAgent(agent: AgentListDto): void {
        this.confirmationService.confirm({
            message: `${agent.ad} agent'ını iptal etmek istediğinize emin misiniz? Bu işlem geri alınamaz.`,
            header: 'İptal Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.revokeAgent(agent.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Agent iptal edildi.' });
                        this.loadAgents();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    openEnrollmentDialog(): void {
        this.enrollmentForm = { kurumId: 0, tesisIds: [], allowedScopes: [] };
        this.enrollmentDialogVisible.set(true);
        this.loadEnrollmentCodes();
    }

    loadEnrollmentCodes(): void {
        this.service.getEnrollmentCodes().subscribe({
            next: (data) => this.enrollmentCodes.set(data),
            error: () => { /* ignnore */ }
        });
    }

    generateEnrollmentCode(): void {
        this.service.generateEnrollmentCode(this.enrollmentForm).subscribe({
            next: (code) => {
                this.messageService.add({ severity: 'success', summary: 'Kod Oluşturuldu', detail: code.code });
                this.loadEnrollmentCodes();
                this.enrollmentForm = { kurumId: 0, tesisIds: [], allowedScopes: [] };
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    revokeEnrollmentCode(code: AgentEnrollmentCodeDto): void {
        this.service.revokeEnrollmentCode(code.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Kod iptal edildi.' });
                this.loadEnrollmentCodes();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getDurumSeverity(durum: number): 'success' | 'warn' | 'danger' | 'info' {
        switch (durum) {
            case 1: return 'success';
            case 0: return 'warn';
            case 2: return 'danger';
            case 3: return 'danger';
            default: return 'info';
        }
    }

    getDurumLabel(durum: number): string {
        return this.durumLabels[durum] ?? 'Bilinmiyor';
    }

    loadCommands(agentId: number): void {
        this.commandsLoading.set(true);
        this.service.getCommands(agentId).pipe(finalize(() => this.commandsLoading.set(false))).subscribe({
            next: (data) => this.commands.set(data),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    sendCommand(): void {
        if (!this.viewingAgentId()) return;
        this.service.sendCommand(this.viewingAgentId()!, {
            agentId: this.viewingAgentId()!,
            commandType: this.selectedCommandType(),
            priority: 1
        }).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Komut gönderildi.' });
                this.loadCommands(this.viewingAgentId()!);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getCommandStatusLabel(status: number): string {
        const labels: Record<number, string> = { 0: 'Pending', 1: 'Delivered', 2: 'Accepted', 3: 'Running', 4: 'Completed', 5: 'Failed', 6: 'Cancelled', 7: 'Expired', 8: 'Rejected' };
        return labels[status] ?? 'Unknown';
    }

    getCommandStatusSeverity(status: number): string {
        switch (status) {
            case 0: case 1: return 'info';
            case 2: case 3: return 'warn';
            case 4: return 'success';
            case 5: case 7: case 8: return 'danger';
            default: return 'secondary';
        }
    }

    closeDialog(): void {
        this.dialogVisible.set(false);
        this.viewingAgentId.set(null);
        this.realtime.leaveAgentGroup();
    }
}

import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, inject, signal, effect, DestroyRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, timer } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { AgentRealtimeService } from '../../core/agent/agent-realtime.service';
import { AuthService } from '../auth/auth.service';
import {
    AgentDto,
    AgentCompatibilityStatusLabels,
    AgentDurumLabels,
    AgentEnrollmentCodeDto,
    AgentEnrollmentCodeRequest,
    AgentListDto,
    AgentKaydetRequest,
    AgentCommandDto
} from './agent-yonetimi.dto';
import { AgentYonetimiService } from './agent-yonetimi.service';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AgentInstallationWizardComponent } from './agent-installation-wizard.component';

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
        TooltipModule,
        TabsModule,
        AgentInstallationWizardComponent
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './agent-yonetimi.html'
})
export class AgentYonetimiComponent implements OnInit, OnDestroy {
    private readonly service = inject(AgentYonetimiService);
    private readonly tesisService = inject(TesisYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly realtime = inject(AgentRealtimeService);
    private readonly authService = inject(AuthService);
    private readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly destroyRef = inject(DestroyRef);

    agents = signal<AgentListDto[]>([]);
    loading = signal(false);
    selectedTesisFilterId = signal<number | null>(null);
    dialogVisible = signal(false);
    enrollmentDialogVisible = signal(false);
    enrollmentCodes = signal<AgentEnrollmentCodeDto[]>([]);
    /** Kurum-wide mandatory approval policy, learned from the codes the backend returns. The
     *  backend is the source of truth; this only drives the hint and the disabled checkbox. */
    kurumRequiresApproval = signal(false);
    submitted = signal(false);
    commands = signal<AgentCommandDto[]>([]);
    commandsLoading = signal(false);
    selectedCommandType = signal<string>('Ping');
    viewingAgentId = signal<number | null>(null);
    selectedAgentDetail = signal<AgentDto | null>(null);
    stagingUpgrade = signal(false);

    agentForm: AgentFormState = { ad: '', tesisIds: [], scopes: [] };
    enrollmentForm: AgentEnrollmentCodeRequest = { tesisIds: [], allowedScopes: [] };
    enrollmentTesisler: TesisDto[] = [];
    readonly enrollmentScopeOptions = [
        { label: 'agent.heartbeat', value: 'agent.heartbeat' },
        { label: 'agent.command.read', value: 'agent.command.read' },
        { label: 'agent.command.execute', value: 'agent.command.execute' },
        { label: 'agent.result.write', value: 'agent.result.write' },
        { label: 'agent.config.read', value: 'agent.config.read' }
    ];

    durumLabels = AgentDurumLabels;
    commandTypes = ['Ping', 'HealthCheck', 'RefreshConfiguration', 'PavoPairing', 'PavoPing', 'PavoGetDeviceInfo'];
    private readonly agentRefreshEffectInitialized = { value: false };
    private readonly refreshIntervalMs = 30000;

    constructor() {
        timer(this.refreshIntervalMs, this.refreshIntervalMs)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(() => this.refreshAgents());

        effect(() => {
            const update = this.realtime.commandUpdates();
            if (update && this.viewingAgentId() && update.agentId === this.viewingAgentId()) {
                this.commands.update(list => {
                    const without = list.filter(c => c.id !== update.id);
                    return [update, ...without];
                });
            }
        });

        effect(() => {
            this.realtime.agentChanged();
            if (!this.agentRefreshEffectInitialized.value) {
                this.agentRefreshEffectInitialized.value = true;
                return;
            }

            this.loadAgents();
        });
    }

    ngOnInit(): void {
        this.loadEnrollmentTesisler();
    }

    ngOnDestroy(): void {
        this.realtime.leaveAgentGroup();
    }

    loadAgents(): void {
        this.loading.set(true);
        this.service.getAgents(this.authService.getAktifKurumId(), this.selectedTesisFilterId()).pipe(finalize(() => this.loading.set(false))).subscribe({
            next: (data) => this.agents.set(data),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    refreshAgents(): void {
        this.loadAgents();
        const viewingAgentId = this.viewingAgentId();
        if (viewingAgentId) {
            this.loadCommands(viewingAgentId);
        }
    }

    openNew(): void {
        this.agentForm = this.createDefaultAgentForm();
        this.selectedAgentDetail.set(null);
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    editAgent(agent: AgentListDto): void {
        this.service.getAgent(agent.id).subscribe({
            next: (detail) => {
                this.selectedAgentDetail.set(detail);
                this.agentForm = {
                    id: detail.id, ad: detail.ad,
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
        if (!this.agentForm.ad) return;

        const request: AgentKaydetRequest = {
            ad: this.agentForm.ad,
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

    rejectAgent(agent: AgentListDto): void {
        this.confirmationService.confirm({
            message: `${agent.ad} agent'ının kaydını reddetmek istediğinize emin misiniz? Agent'ın credential'ı iptal edilecek.`,
            header: 'Red Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.rejectAgent(agent.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Agent kaydı reddedildi.' });
                        this.loadAgents();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
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
        this.enrollmentForm = this.createDefaultEnrollmentForm();
        this.enrollmentDialogVisible.set(true);
        if (this.enrollmentTesisler.length === 0) {
            this.loadEnrollmentTesisler();
            return;
        }

        this.loadEnrollmentCodes();
    }

    loadEnrollmentCodes(): void {
        this.service.getEnrollmentCodes(this.authService.getAktifKurumId(), this.selectedTesisFilterId()).subscribe({
            next: (data) => this.enrollmentCodes.set(data),
            error: () => { /* ignnore */ }
        });
    }

    loadEnrollmentTesisler(): void {
        this.tesisService.getTesisler().subscribe({
            next: (data) => {
                const sorted = [...data].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                this.enrollmentTesisler = sorted;
                const currentFilter = this.selectedTesisFilterId() ?? this.tesisContext.seciliTesis()?.id ?? null;
                if (currentFilter != null && sorted.some((x) => x.id === currentFilter)) {
                    this.selectedTesisFilterId.set(currentFilter);
                } else if (currentFilter == null && sorted.length === 1) {
                    const onlyTesisId = sorted[0]?.id ?? null;
                    this.selectedTesisFilterId.set(onlyTesisId);
                } else if (this.selectedTesisFilterId() != null && !sorted.some((x) => x.id === this.selectedTesisFilterId())) {
                    this.selectedTesisFilterId.set(null);
                }

                if (this.enrollmentForm.tesisIds.length === 0) {
                    const defaultTesisId = this.selectedTesisFilterId() ?? sorted.find((x) => x.id != null)?.id ?? null;
                    if (defaultTesisId != null) {
                        this.enrollmentForm = { ...this.enrollmentForm, tesisIds: [defaultTesisId] };
                    }
                }
                this.loadAgents();
            },
            error: () => {
                this.enrollmentTesisler = [];
                this.loadAgents();
            }
        });
    }

    onTesisFilterChange(): void {
        if (!this.enrollmentDialogVisible()) {
            this.loadAgents();
            this.loadEnrollmentCodes();
            return;
        }

        this.loadAgents();
        this.loadEnrollmentCodes();
    }

    generateEnrollmentCode(): void {
        this.service.generateEnrollmentCode(this.enrollmentForm).subscribe({
            next: (code) => {
                // This is the only moment the plaintext code exists client-side; it is not
                // recoverable from the listing afterwards, so keep the toast up until dismissed.
                this.kurumRequiresApproval.set(code.kurumRequiresApproval === true);
                this.messageService.add({
                    severity: 'success',
                    summary: code.effectiveRequiresApproval
                        ? 'Kod Oluşturuldu — onay gerekecek (yalnızca bir kez gösterilir)'
                        : 'Kod Oluşturuldu (yalnızca bir kez gösterilir)',
                    detail: code.code ?? '',
                    sticky: true
                });
                this.loadEnrollmentCodes();
                this.enrollmentForm = this.createDefaultEnrollmentForm();
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

    getApplyCommandStatus(): { label: string; severity: 'success' | 'info' | 'warn' | 'danger' | 'secondary'; commandId?: string } | null {
        const stagedCommand = [...this.commands()]
            .filter((cmd) => cmd.commandType === 'AgentStageUpgrade' && cmd.status === 4 && !!cmd.resultPayload)
            .sort((left, right) => (right.createdAt ?? '').localeCompare(left.createdAt ?? ''))[0];

        if (!stagedCommand?.resultPayload) {
            return null;
        }

        const parsed = this.tryParseStageResponse(stagedCommand.resultPayload);
        if (!parsed || parsed.stageStatus !== 3) {
            return null;
        }

        const applyCommand = [...this.commands()]
            .filter((cmd) => cmd.commandType === 'AgentApplyUpgrade')
            .sort((left, right) => (right.createdAt ?? '').localeCompare(left.createdAt ?? ''))[0];

        if (!applyCommand) {
            return { label: 'Hazır', severity: 'info', commandId: stagedCommand.id };
        }

        const statusLabel = this.getCommandStatusLabel(applyCommand.status);
        const severity = this.getCommandStatusSeverity(applyCommand.status);
        return { label: statusLabel, severity, commandId: applyCommand.id };
    }

    canStageUpgrade(detail: AgentDto | null): boolean {
        if (!detail) return false;
        return detail.compatibilityStatus === 1 || detail.compatibilityStatus === 2;
    }

    canApplyUpgrade(detail: AgentDto | null): boolean {
        if (!detail || !this.canStageUpgrade(detail)) {
            return false;
        }

        return this.getApplyCommandStatus() !== null;
    }

    stageUpgrade(agentId: number): void {
        this.stagingUpgrade.set(true);
        this.service.stageUpgrade(agentId).pipe(finalize(() => this.stagingUpgrade.set(false))).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Güncelleme hazırlama komutu gönderildi.' });
                this.loadCommands(agentId);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    applyUpgrade(agentId: number): void {
        this.stagingUpgrade.set(true);
        this.service.applyUpgrade(agentId).pipe(finalize(() => this.stagingUpgrade.set(false))).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Güncelleme uygulama komutu gönderildi.' });
                this.loadCommands(agentId);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getCommandStatusSeverity(status: number): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        switch (status) {
            case 0: case 1: return 'info';
            case 2: case 3: return 'warn';
            case 4: return 'success';
            case 5: case 7: case 8: return 'danger';
            default: return 'secondary';
        }
    }

    getCompatibilityLabel(status: number): string {
        return AgentCompatibilityStatusLabels[status] ?? 'Bilinmiyor';
    }

    getCompatibilitySeverity(status: number): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        switch (status) {
            case 1: return 'success';
            case 2: return 'warn';
            case 3:
            case 4: return 'danger';
            default: return 'secondary';
        }
    }

    closeDialog(): void {
        this.dialogVisible.set(false);
        this.viewingAgentId.set(null);
        this.selectedAgentDetail.set(null);
        this.realtime.leaveAgentGroup();
    }

    private createDefaultEnrollmentForm(): AgentEnrollmentCodeRequest {
        const firstTesisId = this.selectedTesisFilterId() ?? this.enrollmentTesisler.find((x) => x.id != null)?.id;
        return {
            tesisIds: firstTesisId != null ? [firstTesisId] : [],
            allowedScopes: this.enrollmentScopeOptions.map((x) => x.value),
            requiresApproval: false
        };
    }

    private createDefaultAgentForm(): AgentFormState {
        return { ad: '', tesisIds: this.selectedTesisFilterId() != null ? [this.selectedTesisFilterId()!] : [], scopes: [] };
    }

    private tryParseStageResponse(payload: string): { stageStatus: number; releaseId?: number; version?: string } | null {
        try {
            const parsed = JSON.parse(payload) as { stageStatus?: number; releaseId?: number; version?: string };
            if (typeof parsed?.stageStatus !== 'number') {
                return null;
            }

            return {
                stageStatus: parsed.stageStatus,
                releaseId: parsed.releaseId,
                version: parsed.version
            };
        } catch {
            return null;
        }
    }
}

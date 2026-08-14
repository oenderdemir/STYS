import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { StepperModule } from 'primeng/stepper';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ToolbarModule } from 'primeng/toolbar';
import {
    AgentInstallationSessionCreateRequest,
    AgentInstallationSessionModel,
    AgentInstallationSessionStatusLabels
} from './agent-yonetimi.dto';
import { AgentYonetimiService } from './agent-yonetimi.service';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';

type WizardFormState = AgentInstallationSessionCreateRequest;

@Component({
    selector: 'app-agent-installation-wizard',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        DialogModule,
        InputTextModule,
        MultiSelectModule,
        SelectModule,
        StepperModule,
        TableModule,
        TagModule,
        TooltipModule,
        ToolbarModule
    ],
    templateUrl: './agent-installation-wizard.component.html'
})
export class AgentInstallationWizardComponent implements OnInit {
    @Input() initialTesisId: number | null = null;

    private readonly service = inject(AgentYonetimiService);
    private readonly tesisService = inject(TesisYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    wizardVisible = signal(false);
    wizardStep = signal<number | undefined>(1);
    wizardLoading = signal(false);
    sessionsLoading = signal(false);
    sessionDetailLoading = signal(false);

    tesisler: TesisDto[] = [];
    installationSessions = signal<AgentInstallationSessionModel[]>([]);
    selectedSession = signal<AgentInstallationSessionModel | null>(null);
    generatedEnrollmentCode = signal<string | null>(null);

    wizardForm: WizardFormState = {
        tesisId: 0,
        agentDisplayName: '',
        targetRid: 'win-x64',
        scopes: [],
        requiresApproval: false
    };

    readonly scopeOptions = [
        { label: 'agent.heartbeat', description: 'Heartbeat', value: 'agent.heartbeat' },
        { label: 'agent.command.read', description: 'Komutları Oku', value: 'agent.command.read' },
        { label: 'agent.command.execute', description: 'Komut Çalıştır', value: 'agent.command.execute' },
        { label: 'agent.result.write', description: 'Sonuç Yaz', value: 'agent.result.write' },
        { label: 'agent.config.read', description: 'Konfigürasyon Oku', value: 'agent.config.read' }
    ];

    readonly platformOptions = [
        { label: 'Windows 64-bit', rid: 'win-x64', hint: 'Windows üzerinde çalışan Agent paketi.' },
        { label: 'Linux 64-bit', rid: 'linux-x64', hint: 'Linux üzerinde çalışan Agent paketi.' }
    ];

    readonly statusLabels = AgentInstallationSessionStatusLabels;
    readonly defaultScopes = this.scopeOptions.map((x) => x.value);

    ngOnInit(): void {
        this.loadTesisler();
        this.loadInstallationSessions();
    }

    openWizard(): void {
        this.resetWizardState();
        this.wizardVisible.set(true);
        this.wizardStep.set(1);
    }

    closeWizard(): void {
        this.wizardVisible.set(false);
        this.resetWizardState();
    }

    loadInstallationSessions(): void {
        this.sessionsLoading.set(true);
        this.service.getInstallations().pipe(finalize(() => this.sessionsLoading.set(false))).subscribe({
            next: (sessions) => this.installationSessions.set(sessions),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    selectSession(session: AgentInstallationSessionModel): void {
        this.selectedSession.set(session);
        this.generatedEnrollmentCode.set(null);
        this.wizardVisible.set(true);
        this.wizardStep.set(6);
        this.loadSessionDetail(session.id);
    }

    createSession(): void {
        if (this.isCreationLocked()) {
            return;
        }

        if (!this.canCreateSession()) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Lütfen tesis, agent adı, yetkiler ve platform seçin.' });
            return;
        }

        this.wizardLoading.set(true);
        const request: AgentInstallationSessionCreateRequest = {
            tesisId: this.wizardForm.tesisId,
            agentDisplayName: this.wizardForm.agentDisplayName.trim(),
            targetRid: this.wizardForm.targetRid,
            scopes: [...this.wizardForm.scopes],
            requiresApproval: this.wizardForm.requiresApproval
        };

        this.service.createInstallation(request).pipe(finalize(() => this.wizardLoading.set(false))).subscribe({
            next: (response) => {
                this.generatedEnrollmentCode.set(response.enrollmentCode);
                this.selectedSession.set(response.session);
                this.wizardStep.set(5);
                this.messageService.add({ severity: 'success', summary: 'Kurulum Oturumu Oluşturuldu', detail: 'Enrollment kodu hazır.' });
                this.loadInstallationSessions();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    refreshSelectedSession(): void {
        const session = this.selectedSession();
        if (!session) {
            return;
        }

        this.loadSessionDetail(session.id);
    }

    cancelSession(session: AgentInstallationSessionModel): void {
        if (this.isTerminalStatus(session.status)) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Kurulum oturumu iptal edilecek ve kullanılmamış enrollment kodu geçersiz hale gelecektir. Eğer Agent zaten kayıt olmuşsa Agent otomatik olarak silinmeyecek veya revoke edilmeyecektir.',
            header: 'Kurulumu İptal Et',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.cancelInstallation(session.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Kurulum oturumu iptal edildi.' });
                        this.loadInstallationSessions();
                        if (this.selectedSession()?.id === session.id) {
                            this.loadSessionDetail(session.id);
                        }
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    copyEnrollmentCode(): void {
        const code = this.generatedEnrollmentCode();
        if (!code || typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
            return;
        }

        navigator.clipboard.writeText(code).then(() => {
            this.messageService.add({ severity: 'success', summary: 'Kopyalandı', detail: 'Enrollment kodu panoya kopyalandı.' });
        });
    }

    goToStep(step: number): void {
        this.wizardStep.set(step);
        if (step === 6) {
            this.refreshSelectedSession();
        }
    }

    getStatusLabel(status: number): string {
        return this.statusLabels[status] ?? 'Bilinmiyor';
    }

    getStatusSeverity(status: number): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        switch (status) {
            case 5:
            case 6:
            case 7:
                return 'success';
            case 1:
            case 2:
            case 3:
                return 'info';
            case 4:
                return 'warn';
            case 8:
            case 9:
            case 10:
                return 'danger';
            default:
                return 'secondary';
        }
    }

    getPlatformLabel(rid: string): string {
        return this.platformOptions.find((x) => x.rid === rid)?.label ?? rid;
    }

    getPlatformHint(rid: string): string {
        return this.platformOptions.find((x) => x.rid === rid)?.hint ?? '';
    }

    getSelectedTesisLabel(): string {
        return this.tesisler.find((x) => x.id === this.wizardForm.tesisId)?.ad ?? '-';
    }

    isTerminalStatus(status: number): boolean {
        return status === 7 || status === 8 || status === 9 || status === 10;
    }

    canCreateSession(): boolean {
        return this.wizardForm.tesisId > 0
            && this.wizardForm.agentDisplayName.trim().length > 0
            && this.wizardForm.targetRid.length > 0
            && this.wizardForm.scopes.length > 0
            && !this.isCreationLocked();
    }

    isSelectedSessionTerminal(): boolean {
        const session = this.selectedSession();
        return !!session && this.isTerminalStatus(session.status);
    }

    isCreationLocked(): boolean {
        return this.generatedEnrollmentCode() !== null || this.selectedSession() !== null;
    }

    private loadTesisler(): void {
        this.tesisService.getTesisler().subscribe({
            next: (data) => {
                this.tesisler = [...data].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                this.seedWizardDefaults();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    private loadSessionDetail(id: number): void {
        this.sessionDetailLoading.set(true);
        this.service.getInstallation(id).pipe(finalize(() => this.sessionDetailLoading.set(false))).subscribe({
            next: (session) => this.selectedSession.set(session),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    private resetWizardState(): void {
        this.generatedEnrollmentCode.set(null);
        this.selectedSession.set(null);
        this.wizardLoading.set(false);
        this.sessionDetailLoading.set(false);
        this.wizardForm = this.createDefaultWizardForm();
    }

    private seedWizardDefaults(): void {
        this.wizardForm = this.createDefaultWizardForm();
    }

    private createDefaultWizardForm(): WizardFormState {
        const selectedTesisId = this.resolveDefaultTesisId();
        const selectedTesis = this.tesisler.find((x) => x.id === selectedTesisId);
        const targetRid = this.resolveDefaultRid();
        return {
            tesisId: selectedTesisId ?? 0,
            agentDisplayName: selectedTesis?.ad ? `${selectedTesis.ad} Agent` : 'Yeni Agent',
            targetRid,
            scopes: [...this.defaultScopes],
            requiresApproval: false
        };
    }

    private resolveDefaultTesisId(): number | null {
        if (this.initialTesisId != null && this.tesisler.some((x) => x.id === this.initialTesisId)) {
            return this.initialTesisId;
        }

        return this.tesisler.find((x) => x.id != null)?.id ?? null;
    }

    private resolveDefaultRid(): string {
        if (typeof navigator !== 'undefined' && navigator.userAgent.toLowerCase().includes('linux')) {
            return 'linux-x64';
        }

        return 'win-x64';
    }
}

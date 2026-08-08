import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
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
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { CheckboxModule } from 'primeng/checkbox';
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
        CheckboxModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './agent-yonetimi.html'
})
export class AgentYonetimiComponent implements OnInit {
    private readonly service = inject(AgentYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    agents = signal<AgentListDto[]>([]);
    loading = signal(false);
    dialogVisible = signal(false);
    enrollmentDialogVisible = signal(false);
    enrollmentCodes = signal<AgentEnrollmentCodeDto[]>([]);
    submitted = signal(false);

    agentForm: AgentFormState = { ad: '', kurumId: 0, tesisIds: [], scopes: [] };
    enrollmentForm: AgentEnrollmentCodeRequest = { kurumId: 0, tesisIds: [], allowedScopes: [] };

    durumLabels = AgentDurumLabels;

    ngOnInit(): void {
        this.loadAgents();
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
                    id: detail.id,
                    ad: detail.ad,
                    kurumId: detail.kurumId,
                    tesisIds: detail.tesisIds,
                    scopes: detail.scopes
                };
                this.submitted.set(false);
                this.dialogVisible.set(true);
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
}

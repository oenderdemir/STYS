import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { AgentYonetimiService } from '../agent-yonetimi/agent-yonetimi.service';
import { AgentListDto } from '../agent-yonetimi/agent-yonetimi.dto';
import { AgentRealtimeService } from '../../core/agent/agent-realtime.service';
import { KasaBankaHesapModel, KasaBankaHesapTipi } from '../muhasebe/kasa-banka-hesaplari/kasa-banka-hesaplari.dto';
import { KasaBankaHesaplariService } from '../muhasebe/kasa-banka-hesaplari/kasa-banka-hesaplari.service';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';
import {
    PosCihaziDto,
    PosCihaziKaydetRequest,
    PosOdemeIslemiDto,
    PosPaymentBaslatRequestDto,
    PosSaglayiciDto,
    PosTerminalDto,
    PosTerminalKaydetRequest,
    SaglayiciLabels
} from './pos-yonetimi.dto';
import { PosYonetimiService } from './pos-yonetimi.service';

type PosCihaziFormState = PosCihaziKaydetRequest & { id?: number };
type PosTerminalFormState = PosTerminalKaydetRequest & { id?: number };
type PosPaymentFormState = PosPaymentBaslatRequestDto;

@Component({
    selector: 'app-pos-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DialogModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        TooltipModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './pos-yonetimi.html',
    styleUrl: './pos-yonetimi.scss'
})
export class PosYonetimiComponent implements OnInit {
    private readonly service = inject(PosYonetimiService);
    private readonly kasaBankaHesapService = inject(KasaBankaHesaplariService);
    private readonly tesisService = inject(TesisYonetimiService);
    private readonly agentService = inject(AgentYonetimiService);
    private readonly agentRealtime = inject(AgentRealtimeService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly handledCommandRefreshKeys = new Set<string>();

    cihazlar = signal<PosCihaziDto[]>([]);
    tesisler = signal<TesisDto[]>([]);
    agents = signal<AgentListDto[]>([]);
    selectedTesisFilterId = signal<number | null>(null);
    cihazLoading = signal(false);
    dialogVisible = signal(false);
    submitted = signal(false);
    selectedCihaz = signal<PosCihaziDto | null>(null);

    saglayicilar = signal<PosSaglayiciDto[]>([]);
    krediKartiHesaplari = signal<KasaBankaHesapModel[]>([]);
    terminals = signal<PosTerminalDto[]>([]);
    terminalsLoading = signal(false);
    terminalDialogVisible = signal(false);
    terminalSubmitted = signal(false);
    terminalSaving = signal(false);
    paymentTests = signal<PosOdemeIslemiDto[]>([]);
    paymentTestsLoading = signal(false);
    paymentSaving = signal(false);
    currentPaymentTest = signal<PosOdemeIslemiDto | null>(null);
    paymentSubmitted = signal(false);

    form: PosCihaziFormState = { tesisId: 0, saglayici: 0, ad: '', seriNo: '' };
    terminalForm: PosTerminalFormState = this.createEmptyTerminalForm();
    paymentForm: PosPaymentFormState = this.createEmptyPaymentForm();

    readonly cihazSaglayiciOptions = [{ label: 'PAVO', value: 0 }, { label: 'Diğer', value: 1 }];
    readonly filteredCihazlar = computed(() => {
        const tesisId = this.selectedTesisFilterId();
        if (!tesisId) {
            return this.cihazlar();
        }

        return this.cihazlar().filter((item) => item.tesisId === tesisId);
    });

    readonly cihazOzet = computed(() => {
        const cihazlar = this.cihazlar();
        return {
            toplam: cihazlar.length,
            aktif: cihazlar.filter((item) => item.aktifMi).length,
            eslesmis: cihazlar.filter((item) => item.eslesmeOnayliMi).length,
            terminal: cihazlar.reduce((sum, item) => sum + (item.terminalSayisi ?? 0), 0)
        };
    });

    readonly tesisFilterOptions = computed(() => [
        { label: 'Tüm tesisler', value: null },
        ...this.tesisler().map((item) => ({ label: item.ad, value: item.id ?? null }))
    ]);

    constructor() {
        effect(() => {
            const update = this.agentRealtime.commandUpdates();
            const cihaz = this.selectedCihaz();

            if (!update || !cihaz?.agentId) {
                return;
            }

            if (update.agentId !== cihaz.agentId) {
                return;
            }

            if (!['PavoPairing', 'PavoPing', 'PavoGetDeviceInfo', 'PavoStartPayment', 'PavoGetPaymentResult'].includes(update.commandType)) {
                return;
            }

            if (![1, 2, 3, 4, 5, 6, 7, 8].includes(update.status)) {
                return;
            }

            const key = `${update.id}:${update.status}`;
            if (this.handledCommandRefreshKeys.has(key)) {
                return;
            }

            this.handledCommandRefreshKeys.add(key);
            this.reloadSelectedDevice(cihaz.id);
        });
    }

    ngOnInit(): void {
        this.load();
        this.loadSaglayicilar();
        this.loadTesisler();
        this.loadAgents();
        this.loadKrediKartiHesaplari();
    }

    load(): void {
        this.cihazLoading.set(true);
        this.service.getAll().pipe(finalize(() => this.cihazLoading.set(false))).subscribe({
            next: (items) => this.cihazlar.set(items),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    loadSaglayicilar(): void {
        this.service.getSaglayicilar().subscribe({
            next: (items) => this.saglayicilar.set(items),
            error: () => this.saglayicilar.set([])
        });
    }

    loadTesisler(): void {
        this.tesisService.getTesisler().subscribe({
            next: (items) => {
                this.tesisler.set(items);
                const selectedFilterId = this.selectedTesisFilterId();
                if (selectedFilterId != null && !items.some((item) => item.id === selectedFilterId)) {
                    this.selectedTesisFilterId.set(null);
                }
            },
            error: () => this.tesisler.set([])
        });
    }

    loadAgents(): void {
        this.agentService.getAgents().subscribe({
            next: (items) => this.agents.set(items),
            error: () => this.agents.set([])
        });
    }

    loadKrediKartiHesaplari(): void {
        this.kasaBankaHesapService.getByTip('KrediKarti' as KasaBankaHesapTipi, true).subscribe({
            next: (items) => this.krediKartiHesaplari.set(items),
            error: () => this.krediKartiHesaplari.set([])
        });
    }

    openNew(): void {
        this.form = {
            tesisId: this.selectedTesisFilterId() ?? this.tesisler()[0]?.id ?? 0,
            saglayici: 0,
            ad: '',
            seriNo: '',
            agentId: undefined
        };
        this.selectedCihaz.set(null);
        this.agentRealtime.leaveAgentGroup();
        this.terminals.set([]);
        this.paymentTests.set([]);
        this.currentPaymentTest.set(null);
        this.paymentForm = this.createEmptyPaymentForm();
        this.paymentSubmitted.set(false);
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    edit(cihaz: PosCihaziDto): void {
        this.service.getById(cihaz.id).subscribe({
            next: (detail) => {
                this.selectedCihaz.set(detail);
                this.handledCommandRefreshKeys.clear();
                this.paymentForm = this.createEmptyPaymentForm();
                this.paymentSubmitted.set(false);
                if (detail.agentId) {
                    this.agentRealtime.joinAgentGroup(detail.agentId);
                } else {
                    this.agentRealtime.leaveAgentGroup();
                }
                this.form = {
                    id: detail.id,
                    tesisId: detail.tesisId,
                    agentId: detail.agentId,
                    saglayici: detail.saglayici,
                    ad: detail.ad,
                    seriNo: detail.seriNo,
                    ipAdresi: detail.ipAdresi,
                    httpPort: detail.httpPort,
                    httpsPort: detail.httpsPort,
                    fingerprint: detail.fingerprint,
                    aciklama: detail.aciklama
                };
                this.submitted.set(false);
                this.dialogVisible.set(true);
                this.loadTerminals(detail.id);
                this.loadPaymentTests(detail.id);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    save(): void {
        this.submitted.set(true);
        if (!this.form.ad.trim() || !this.form.seriNo.trim() || this.form.tesisId <= 0) {
            return;
        }

        const request: PosCihaziKaydetRequest = {
            tesisId: this.form.tesisId,
            agentId: this.form.agentId,
            saglayici: this.form.saglayici,
            ad: this.form.ad.trim(),
            seriNo: this.form.seriNo.trim(),
            ipAdresi: this.form.ipAdresi,
            httpPort: this.form.httpPort,
            httpsPort: this.form.httpsPort,
            fingerprint: this.form.fingerprint,
            aciklama: this.form.aciklama
        };

        const action$ = this.form.id ? this.service.update(this.form.id, request) : this.service.create(request);
        action$.subscribe({
            next: (saved) => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'POS cihazı kaydedildi.' });
                this.selectedCihaz.set(saved);
                this.handledCommandRefreshKeys.clear();
                this.paymentForm = this.createEmptyPaymentForm();
                this.paymentSubmitted.set(false);
                if (saved.agentId) {
                    this.agentRealtime.joinAgentGroup(saved.agentId);
                }
                this.form.id = saved.id;
                this.dialogVisible.set(true);
                this.load();
                this.loadTerminals(saved.id);
                this.loadPaymentTests(saved.id);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    deleteItem(cihaz: PosCihaziDto): void {
        this.confirmationService.confirm({
            message: `"${cihaz.ad}" cihazını silmek istediğinize emin misiniz?`,
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            accept: () => this.service.delete(cihaz.id).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Cihaz pasifleştirildi.' });
                    if (this.selectedCihaz()?.id === cihaz.id) {
                        this.closeDialog();
                    }
                    this.load();
                },
                error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
            })
        });
    }

    closeDialog(): void {
        this.dialogVisible.set(false);
        this.selectedCihaz.set(null);
        this.agentRealtime.leaveAgentGroup();
        this.terminals.set([]);
        this.paymentTests.set([]);
        this.currentPaymentTest.set(null);
        this.paymentForm = this.createEmptyPaymentForm();
        this.paymentSubmitted.set(false);
        this.terminalDialogVisible.set(false);
    }

    getSaglayiciLabel(saglayici: number): string {
        return SaglayiciLabels[saglayici] ?? '?';
    }

    getTesisLabel(tesisId: number | null | undefined): string {
        return this.tesisler().find((item) => item.id === tesisId)?.ad ?? '-';
    }

    getAgentLabel(agentId: number | null | undefined): string {
        const agent = this.agents().find((item) => item.id === agentId);
        if (!agent) {
            return '-';
        }

        return agent.kurumAd ? `${agent.ad} • ${agent.kurumAd}` : agent.ad;
    }

    loadTerminals(cihazId: number): void {
        this.terminalsLoading.set(true);
        this.service.getTerminals(cihazId).pipe(finalize(() => this.terminalsLoading.set(false))).subscribe({
            next: (items) => {
                this.terminals.set(items);
                if (items.length > 0 && !items.some((item) => item.id === this.paymentForm.posTerminalId)) {
                    this.paymentForm.posTerminalId = items[0]?.id ?? 0;
                }
                if ((this.paymentForm.posTerminalId ?? 0) <= 0 && items.length > 0) {
                    this.paymentForm.posTerminalId = items[0]?.id ?? 0;
                }
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    private reloadSelectedDevice(cihazId: number): void {
        this.service.getById(cihazId).subscribe({
            next: (detail) => {
                this.selectedCihaz.set(detail);
                if (detail.agentId) {
                    this.agentRealtime.joinAgentGroup(detail.agentId);
                } else {
                    this.agentRealtime.leaveAgentGroup();
                }
                this.loadTerminals(cihazId);
                this.loadPaymentTests(cihazId);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    openNewTerminal(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalForm = this.createEmptyTerminalForm(cihaz);
        this.terminalSubmitted.set(false);
        this.terminalDialogVisible.set(true);
    }

    editTerminal(terminal: PosTerminalDto): void {
        this.terminalForm = {
            id: terminal.id,
            posCihaziId: terminal.posCihaziId ?? this.selectedCihaz()?.id ?? null,
            kasaBankaHesapId: terminal.kasaBankaHesapId ?? null,
            saglayiciKodu: terminal.saglayiciKodu,
            ad: terminal.ad,
            terminalId: terminal.terminalId,
            merchantId: terminal.merchantId ?? terminal.sourceTerminalReference ?? null,
            serialNumber: terminal.serialNumber,
            sourceFingerprint: terminal.sourceFingerprint ?? null,
            sourceTerminalReference: terminal.sourceTerminalReference ?? null,
            aktifMi: terminal.aktifMi
        };
        this.terminalSubmitted.set(false);
        this.terminalDialogVisible.set(true);
    }

    saveTerminal(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalSubmitted.set(true);
        if (!this.terminalForm.ad.trim() || !this.terminalForm.terminalId.trim() || !this.terminalForm.saglayiciKodu.trim()) {
            return;
        }

        const request: PosTerminalKaydetRequest = {
            posCihaziId: cihaz.id,
            kasaBankaHesapId: this.terminalForm.kasaBankaHesapId ?? null,
            saglayiciKodu: this.terminalForm.saglayiciKodu.trim(),
            ad: this.terminalForm.ad.trim(),
            terminalId: this.terminalForm.terminalId.trim(),
            merchantId: this.terminalForm.merchantId?.trim() || null,
            serialNumber: this.terminalForm.terminalId.trim(),
            sourceFingerprint: this.terminalForm.sourceFingerprint?.trim() || null,
            sourceTerminalReference: this.terminalForm.merchantId?.trim() || this.terminalForm.sourceTerminalReference?.trim() || null,
            aktifMi: this.terminalForm.aktifMi
        };

        this.terminalSaving.set(true);
        const action$ = this.terminalForm.id
            ? this.service.updateTerminal(cihaz.id, this.terminalForm.id, request)
            : this.service.createTerminal(cihaz.id, request);

        action$.pipe(finalize(() => this.terminalSaving.set(false))).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Terminal kaydedildi.' });
                this.terminalDialogVisible.set(false);
                this.loadTerminals(cihaz.id);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    deleteTerminal(terminal: PosTerminalDto): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.confirmationService.confirm({
            message: `"${terminal.ad}" terminalini silmek istediğinize emin misiniz?`,
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            accept: () => this.service.deleteTerminal(cihaz.id, terminal.id).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Terminal pasifleştirildi.' });
                    this.loadTerminals(cihaz.id);
                },
                error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
            })
        });
    }

    startPairing(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalSaving.set(true);
        this.service.startPairing(cihaz.id).pipe(finalize(() => this.terminalSaving.set(false))).subscribe({
            next: () => this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Eşleştirme komutu agent’a iletildi.' }),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    ping(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalSaving.set(true);
        this.service.ping(cihaz.id).pipe(finalize(() => this.terminalSaving.set(false))).subscribe({
            next: () => this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Bağlantı testi agent’a iletildi.' }),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getDeviceInfo(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalSaving.set(true);
        this.service.getDeviceInfo(cihaz.id).pipe(finalize(() => this.terminalSaving.set(false))).subscribe({
            next: () => this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Cihaz bilgisi alma komutu agent’a iletildi.' }),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    syncTerminals(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.terminalSaving.set(true);
        this.service.syncTerminals(cihaz.id).pipe(finalize(() => this.terminalSaving.set(false))).subscribe({
            next: () => this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Terminal senkronizasyonu agent’a iletildi.' }),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    loadPaymentTests(cihazId: number): void {
        this.paymentTestsLoading.set(true);
        this.service.getPaymentTests(cihazId).pipe(finalize(() => this.paymentTestsLoading.set(false))).subscribe({
            next: (items) => {
                this.paymentTests.set(items);
                const currentId = this.currentPaymentTest()?.id;
                if (currentId != null) {
                    const current = items.find((item) => item.id === currentId);
                    if (current) {
                        this.currentPaymentTest.set(current);
                        return;
                    }
                }

                this.currentPaymentTest.set(items[0] ?? null);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    startPaymentTest(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.paymentSubmitted.set(true);
        if (!this.paymentForm.posTerminalId || this.paymentForm.tutar <= 0) {
            return;
        }

        const request: PosPaymentBaslatRequestDto = {
            posTerminalId: this.paymentForm.posTerminalId,
            tutar: this.paymentForm.tutar,
            paraBirimi: this.paymentForm.paraBirimi?.trim() || 'TRY',
            aciklama: this.paymentForm.aciklama?.trim() || null,
            posOdemeIslemiId: this.paymentForm.posOdemeIslemiId ?? null
        };

        this.paymentSaving.set(true);
        this.service.startPaymentTest(cihaz.id, request).pipe(finalize(() => this.paymentSaving.set(false))).subscribe({
            next: (payment) => {
                this.paymentForm.posOdemeIslemiId = payment.id;
                this.currentPaymentTest.set(payment);
                this.upsertPaymentTest(payment);
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Ödeme başlatma komutu gönderildi.' });
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    queryPaymentTestResult(payment: PosOdemeIslemiDto): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id || !payment.id) {
            return;
        }

        this.paymentSaving.set(true);
        this.service.getPaymentTestResult(cihaz.id, payment.id).pipe(finalize(() => this.paymentSaving.set(false))).subscribe({
            next: (updated) => {
                this.currentPaymentTest.set(updated);
                this.upsertPaymentTest(updated);
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Ödeme sonucu sorgulandı.' });
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getTerminalSaglayiciLabel(kod: string): string {
        return this.saglayicilar().find((item) => item.kod === kod)?.ad ?? kod;
    }

    getPaymentTerminalLabel(payment: PosOdemeIslemiDto): string {
        const terminal = this.terminals().find((item) => item.id === payment.posTerminalId);
        if (terminal) {
            return `${terminal.ad} • ${terminal.terminalId}`;
        }

        return payment.terminalId ?? `Terminal #${payment.posTerminalId}`;
    }

    getPaymentTerminalOptions(): Array<{ label: string; value: number }> {
        return this.terminals().map((terminal) => ({
            label: `${terminal.ad} • ${terminal.terminalId}${terminal.kasaBankaHesapAd ? ` • ${terminal.kasaBankaHesapAd}` : ''}`,
            value: terminal.id
        }));
    }

    getPaymentStatusSeverity(durum: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (durum) {
            case 'Successful':
            case 'Basarili':
            case 'Muhasebelestirildi':
                return 'success';
            case 'Processing':
            case 'Pending':
            case 'SentToAgent':
                return 'info';
            case 'Unknown':
                return 'warn';
            case 'Failed':
            case 'Basarisiz':
            case 'MutabakatGerekli':
                return 'danger';
            default:
                return 'secondary';
        }
    }

    getKrediKartiHesapOptions(): Array<{ label: string; value: number }> {
        const cihaz = this.selectedCihaz();
        return this.krediKartiHesaplari()
            .filter((hesap) => !cihaz?.tesisId || hesap.tesisId === cihaz.tesisId)
            .map((hesap) => {
                const tesisAdi = this.getTesisLabel(hesap.tesisId);
                const tesisEtiketi = tesisAdi && tesisAdi !== '-' ? ` • ${tesisAdi}` : '';
                return { label: `${hesap.ad}${hesap.kod ? ` (${hesap.kod})` : ''}${tesisEtiketi}`, value: hesap.id! };
            });
    }

    getSaglayiciOptions(): Array<{ label: string; value: string }> {
        return this.saglayicilar().map((item) => ({ label: `${item.ad} (${item.kod})`, value: item.kod }));
    }

    getAgentOptions(): Array<{ label: string; value: number }> {
        return this.agents().map((agent) => ({ label: `${agent.ad}${agent.kurumAd ? ` • ${agent.kurumAd}` : ''}`, value: agent.id }));
    }

    onTesisChanged(): void {
        const currentAgentId = this.form.agentId;
        if (currentAgentId == null) {
            return;
        }

        const valid = this.getAgentOptions().some((option) => option.value === currentAgentId);
        if (!valid) {
            this.form.agentId = undefined;
        }
    }

    private upsertPaymentTest(payment: PosOdemeIslemiDto): void {
        const current = this.paymentTests();
        const existingIndex = current.findIndex((item) => item.id === payment.id);
        const next = existingIndex >= 0
            ? current.map((item) => item.id === payment.id ? payment : item)
            : [payment, ...current];
        this.paymentTests.set(next.slice(0, 5));
    }

    private createEmptyTerminalForm(cihaz?: PosCihaziDto | null): PosTerminalFormState {
        const ilkSaglayici = this.saglayicilar()[0]?.kod ?? 'PAVO';
        return {
            posCihaziId: cihaz?.id ?? null,
            kasaBankaHesapId: null,
            saglayiciKodu: ilkSaglayici,
            ad: '',
            terminalId: '',
            merchantId: '',
            serialNumber: '',
            sourceFingerprint: '',
            sourceTerminalReference: '',
            aktifMi: true
        };
    }

    private createEmptyPaymentForm(): PosPaymentFormState {
        return {
            posTerminalId: 0,
            tutar: 100,
            paraBirimi: 'TRY',
            aciklama: 'PAVO test ödemesi',
            posOdemeIslemiId: null
        };
    }
}

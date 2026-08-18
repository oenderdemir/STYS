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
import { AuthService } from '../auth/auth.service';
import { KasaBankaHesapModel, KasaBankaHesapTipi } from '../muhasebe/kasa-banka-hesaplari/kasa-banka-hesaplari.dto';
import { KasaBankaHesaplariService } from '../muhasebe/kasa-banka-hesaplari/kasa-banka-hesaplari.service';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';
import { MuhasebeTesisContextService } from '../muhasebe/services/muhasebe-tesis-context.service';
import {
    PosCihaziDto,
    PosCihaziKaydetRequest,
    PosGunSonuIslemiDto,
    PosGunSonuSlipiDto,
    PosOperationalReadinessDto,
    PosOdemeIslemiDto,
    PosOdemeSlipDto,
    PosPaymentBaslatRequestDto,
    PosSaglayiciDto,
    PosTerminalDto,
    PosTerminalOperationalReadinessDto,
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
    private readonly authService = inject(AuthService);
    private readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly handledCommandRefreshKeys = new Set<string>();

    cihazlar = signal<PosCihaziDto[]>([]);
    tesisler = signal<TesisDto[]>([]);
    agents = signal<AgentListDto[]>([]);
    selectedTesisFilterId = signal<number | null>(this.tesisContext.seciliTesis()?.id ?? null);
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

    receiptDialogVisible = signal(false);
    receiptImageUrl = signal<string | null>(null);
    receiptImageLoading = signal(false);

    gunSonuList = signal<PosGunSonuIslemiDto[]>([]);
    gunSonuLoading = signal(false);
    gunSonuSaving = signal(false);
    eodForm = { useSummary: true, print: false };
    eodSlipDialogVisible = signal(false);
    eodSlipList = signal<PosGunSonuSlipiDto[]>([]);
    eodSlipLoading = signal(false);
    eodReceiptImageUrl = signal<string | null>(null);

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

    readonly selectedReadiness = signal<PosOperationalReadinessDto | null>(null);

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
        this.loadTesisler();
        this.loadSaglayicilar();
        this.loadKrediKartiHesaplari();
    }

    load(): void {
        this.cihazLoading.set(true);
        this.service.getAll(this.authService.getAktifKurumId(), this.selectedTesisFilterId()).pipe(finalize(() => this.cihazLoading.set(false))).subscribe({
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
                const contextTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                if (selectedFilterId == null && contextTesisId != null && items.some((item) => item.id === contextTesisId)) {
                    this.selectedTesisFilterId.set(contextTesisId);
                } else if (selectedFilterId != null && !items.some((item) => item.id === selectedFilterId)) {
                    this.selectedTesisFilterId.set(null);
                }

                this.load();
                this.loadAgents();
            },
            error: () => {
                this.tesisler.set([]);
                this.load();
                this.loadAgents();
            }
        });
    }

    loadAgents(): void {
        this.agentService.getAgents(this.authService.getAktifKurumId(), this.selectedTesisFilterId()).subscribe({
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
                this.selectedReadiness.set(null);
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
                this.loadReadiness(detail.id);
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
                this.loadReadiness(saved.id);
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
        this.selectedReadiness.set(null);
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
                this.syncPaymentTerminalSelection(items);
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    loadReadiness(cihazId: number): void {
        this.service.getReadiness(cihazId).subscribe({
            next: (readiness) => {
                this.selectedReadiness.set(readiness);
                this.syncPaymentTerminalSelection(this.terminals());
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
                this.loadEodHistory(cihazId);
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
        const disabledReason = this.getPaymentStartDisabledReason();
        if (!cihaz?.id) {
            return;
        }

        if (disabledReason) {
            this.messageService.add({ severity: 'warn', summary: 'Ödeme başlatılamadı', detail: disabledReason });
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
            posOdemeIslemiId: this.paymentForm.posOdemeIslemiId ?? null,
            idempotencyKey: this.paymentForm.idempotencyKey
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

    getSlipByTip(payment: PosOdemeIslemiDto, tip: number): PosOdemeSlipDto | undefined {
        return payment.slipler?.find((item) => item.tip === tip);
    }

    recoverReceipts(payment: PosOdemeIslemiDto): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id || !payment.id) {
            return;
        }

        this.paymentSaving.set(true);
        this.service.recoverReceipts(cihaz.id, payment.id).pipe(finalize(() => this.paymentSaving.set(false))).subscribe({
            next: (updated) => {
                this.currentPaymentTest.set(updated);
                this.upsertPaymentTest(updated);
                this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Slip kurtarma komutu agent’a iletildi.' });
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    getSlipTipLabel(tip: number): string {
        switch (tip) {
            case 1: return 'Müşteri Slipi';
            case 2: return 'İşyeri Slipi';
            case 3: return 'Hata Slipi';
            default: return 'Slip';
        }
    }

    openReceipt(payment: PosOdemeIslemiDto, tip: number): void {
        const slip = this.getSlipByTip(payment, tip);
        if (!slip) {
            return;
        }

        this.receiptImageLoading.set(true);
        this.receiptDialogVisible.set(true);
        this.service.getReceiptContent(payment.id, slip.id).pipe(finalize(() => this.receiptImageLoading.set(false))).subscribe({
            next: (blob) => {
                this.closeReceiptUrl();
                this.receiptImageUrl.set(URL.createObjectURL(blob));
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message ?? 'Slip görüntülenemedi.' });
                this.receiptDialogVisible.set(false);
            }
        });
    }

    closeReceiptDialog(): void {
        this.receiptDialogVisible.set(false);
        this.closeReceiptUrl();
    }

    private closeReceiptUrl(): void {
        const url = this.receiptImageUrl();
        if (url) {
            URL.revokeObjectURL(url);
        }
        this.receiptImageUrl.set(null);
    }

    // ------------------------------ Gün Sonu ------------------------------

    loadEodHistory(cihazId: number): void {
        this.gunSonuLoading.set(true);
        this.service.getEodHistory(cihazId, 10).pipe(finalize(() => this.gunSonuLoading.set(false))).subscribe({
            next: (items) => this.gunSonuList.set(items),
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    openEodConfirm(): void {
        this.confirmationService.confirm({
            header: 'Gün Sonu Onayı',
            message: 'Bu işlem PAVO cihazında gün sonu işlemini başlatacaktır. Gün sonu tamamlandığında POS batch durumu değişecektir. Devam etmek istiyor musunuz?',
            icon: 'pi pi-exclamation-triangle',
            accept: () => this.startEod()
        });
    }

    startEod(): void {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return;
        }

        this.gunSonuSaving.set(true);
        this.service.startEod(cihaz.id, { useSummary: this.eodForm.useSummary, print: this.eodForm.print })
            .pipe(finalize(() => this.gunSonuSaving.set(false)))
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Komut gönderildi', detail: 'Gün sonu komutu agent’a iletildi.' });
                    this.loadEodHistory(cihaz.id);
                },
                error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
            });
    }

    getEodDurumText(durum: number): string {
        switch (durum) {
            case 0: return 'Bekliyor';
            case 1: return 'Başarılı';
            case 2: return 'Başarısız';
            case 3: return 'Doğrulanamadı';
            default: return 'Bilinmiyor';
        }
    }

    getEodTurText(useSummary: boolean): string {
        return useSummary ? 'Özet' : 'Detay';
    }

    openEodSlips(eod: PosGunSonuIslemiDto): void {
        this.eodSlipList.set([]);
        this.eodSlipLoading.set(true);
        this.eodSlipDialogVisible.set(true);
        this.service.getEodReceipts(eod.id).pipe(finalize(() => this.eodSlipLoading.set(false))).subscribe({
            next: (items) => this.eodSlipList.set(items),
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                this.eodSlipDialogVisible.set(false);
            }
        });
    }

    viewEodSlip(slip: PosGunSonuSlipiDto): void {
        this.service.getEodReceiptContent(slip.posGunSonuIslemiId, slip.id).subscribe({
            next: (blob) => {
                const old = this.eodReceiptImageUrl();
                if (old) {
                    URL.revokeObjectURL(old);
                }
                this.eodReceiptImageUrl.set(URL.createObjectURL(blob));
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message ?? 'Slip görüntülenemedi.' })
        });
    }

    closeEodSlipDialog(): void {
        this.eodSlipDialogVisible.set(false);
        const old = this.eodReceiptImageUrl();
        if (old) {
            URL.revokeObjectURL(old);
        }
        this.eodReceiptImageUrl.set(null);
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

    getPaymentAccountLabel(payment: PosOdemeIslemiDto): string {
        const terminal = this.terminals().find((item) => item.id === payment.posTerminalId);
        if (terminal?.kasaBankaHesapAd) {
            return terminal.kasaBankaHesapAd;
        }

        if (terminal?.kasaBankaHesapId) {
            return `Hesap #${terminal.kasaBankaHesapId}`;
        }

        return 'Hesap eşleştirilmedi';
    }

    getPaymentTerminalOptions(): Array<{ label: string; value: number }> {
        return this.terminals().map((terminal) => ({
            label: `${terminal.ad} • ${terminal.terminalId}${terminal.kasaBankaHesapAd ? ` • ${terminal.kasaBankaHesapAd}` : ''}`,
            value: terminal.id
        }));
    }

    getPaymentStartDisabledReason(): string | null {
        const cihaz = this.selectedCihaz();
        if (!cihaz?.id) {
            return 'Ödeme başlatmak için önce bir cihaz seçin.';
        }

        const terminal = this.terminals().find((item) => item.id === this.paymentForm.posTerminalId);
        if (!terminal) {
            return 'Ödeme için bir terminal seçin.';
        }

        if (!terminal.posCihaziId || terminal.posCihaziId !== cihaz.id) {
            return 'Seçili terminal bu cihaza bağlı değil.';
        }

        if (!terminal.kasaBankaHesapId) {
            return 'Terminal için kredi kartı hesabı eşleştirilmemiş.';
        }

        if (this.paymentForm.tutar <= 0) {
            return 'Tutar sıfırdan büyük olmalıdır.';
        }

        return null;
    }

    getTerminalReadiness(terminalId: number): PosTerminalOperationalReadinessDto | null {
        return this.selectedReadiness()?.terminals.find((item) => item.id === terminalId) ?? null;
    }

    getReadinessSeverity(status: PosOperationalReadinessDto['status']): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (status) {
            case 'Ready':
                return 'success';
            case 'AgentOffline':
            case 'DeviceOffline':
            case 'NoActiveTerminal':
            case 'NoAccountMapping':
            case 'ReProvisionRequired':
            case 'PairingInvalid':
            case 'NotProvisioned':
                return 'warn';
            case 'Disabled':
            case 'OwnershipConflict':
                return 'danger';
            default:
                return 'secondary';
        }
    }

    getReadinessLabel(status: PosOperationalReadinessDto['status']): string {
        switch (status) {
            case 'Ready':
                return 'Ödeme Hazır';
            case 'AgentOffline':
                return 'Agent Çevrimdışı';
            case 'DeviceOffline':
                return 'PAVO Çevrimdışı';
            case 'NotProvisioned':
                return 'Provisioned Değil';
            case 'ReProvisionRequired':
                return 'Yeniden Eşitleme Gerekli';
            case 'PairingInvalid':
                return 'Pairing Geçersiz';
            case 'NoActiveTerminal':
                return 'Aktif Terminal Yok';
            case 'NoAccountMapping':
                return 'Hesap Eşleşmemiş';
            case 'Disabled':
                return 'Devre Dışı';
            case 'OwnershipConflict':
                return 'Sahiplik Çakışması';
            default:
                return 'Bilinmiyor';
        }
    }

    getHealthLabel(status?: PosOperationalReadinessDto['deviceHealthStatus'] | string | null): string {
        switch (status) {
            case 'Healthy':
                return 'Sağlıklı';
            case 'Stale':
                return 'Eski';
            case 'Timeout':
                return 'Zaman Aşımı';
            case 'Unreachable':
                return 'Ulaşılamıyor';
            case 'TlsError':
                return 'TLS Hatası';
            case 'ProtocolError':
                return 'Protokol Hatası';
            case 'Unknown':
            default:
                return 'Bilinmiyor';
        }
    }

    getHealthSeverity(status?: PosOperationalReadinessDto['deviceHealthStatus'] | string | null): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (status) {
            case 'Healthy':
                return 'success';
            case 'Stale':
                return 'warn';
            case 'Timeout':
            case 'Unreachable':
            case 'TlsError':
            case 'ProtocolError':
                return 'danger';
            case 'Unknown':
            default:
                return 'secondary';
        }
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
            this.loadAgents();
            return;
        }

        const valid = this.getAgentOptions().some((option) => option.value === currentAgentId);
        if (!valid) {
            this.form.agentId = undefined;
        }

        this.loadAgents();
    }

    onTesisFilterChanged(): void {
        this.load();
        this.loadAgents();
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
            tutar: 1,
            paraBirimi: 'TRY',
            aciklama: 'PAVO test ödemesi',
            posOdemeIslemiId: null,
            idempotencyKey: this.createPaymentIdempotencyKey()
        };
    }

    touchPaymentAttempt(): void {
        this.paymentForm.posOdemeIslemiId = null;
        this.paymentForm.idempotencyKey = this.createPaymentIdempotencyKey();
    }

    private createPaymentIdempotencyKey(): string {
        const crypto = globalThis.crypto;
        if (crypto?.randomUUID) {
            return crypto.randomUUID().replace(/-/g, '');
        }

        return `pay-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    }

    private isAgentLikelyOffline(sonBaglantiTarihi?: string): boolean {
        if (!sonBaglantiTarihi) {
            return true;
        }

        const lastSeen = new Date(sonBaglantiTarihi);
        if (Number.isNaN(lastSeen.getTime())) {
            return true;
        }

        return Date.now() - lastSeen.getTime() > 5 * 60 * 1000;
    }

    private syncPaymentTerminalSelection(items: PosTerminalDto[]): void {
        if (items.length === 0) {
            this.paymentForm.posTerminalId = 0;
            return;
        }

        if (items.some((item) => item.id === this.paymentForm.posTerminalId)) {
            return;
        }

        const readiness = this.selectedReadiness();
        const readyTerminal = readiness?.terminals.find((item) => item.paymentReady && items.some((terminal) => terminal.id === item.id));
        if (readyTerminal) {
            this.paymentForm.posTerminalId = readyTerminal.id;
            return;
        }

        this.paymentForm.posTerminalId = items[0]?.id ?? 0;
    }
}

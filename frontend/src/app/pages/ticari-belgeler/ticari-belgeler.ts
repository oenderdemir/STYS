import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { toLocalDateString } from '../../core/utils/date-time.util';
import { AuthService } from '../auth';
import { TicariBelgeDetayDialogComponent } from './components/ticari-belge-detay-dialog/ticari-belge-detay-dialog';
import { TicariBelgeGuncelleDialogComponent } from './components/ticari-belge-guncelle-dialog/ticari-belge-guncelle-dialog';
import {
    FATURALAMA_DURUMU_LABELS,
    FATURALAMA_DURUMU_SECENEKLERI,
    FATURALAMA_DURUMU_SEVERITIES,
    MUHASEBE_DURUMU_LABELS,
    MUHASEBE_DURUMU_SECENEKLERI,
    MUHASEBE_DURUMU_SEVERITIES,
    SATIS_BELGESI_TIPI_LABELS,
    SATIS_BELGESI_TIPI_SECENEKLERI,
    SATIS_KAYNAK_MODULU_LABELS,
    TICARI_BELGE_DURUM_SECENEKLERI,
    TICARI_BELGE_DURUMU_LABELS,
    TICARI_BELGE_DURUMU_SEVERITIES,
    TagSeverity,
    TicariBelgeDetayDto,
    TicariBelgeDto,
    TicariBelgeFaturalamaDurumu,
    TicariBelgeFilterDto,
    TicariBelgeGuncelleRequest,
    TicariBelgeMuhasebeDurumu,
    SatisBelgesiTipi,
    SatisKaynakModulu,
    belgeToGuncelleRequest,
    createDefaultTicariBelgeFilter,
    getMusteriDisplayName
} from './ticari-belge.models';
import { TicariBelgeService } from './ticari-belge.service';

@Component({
    selector: 'app-ticari-belgeler',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DatePickerModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        TooltipModule,
        TicariBelgeDetayDialogComponent,
        TicariBelgeGuncelleDialogComponent
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './ticari-belgeler.html'
})
export class TicariBelgelerComponent implements OnInit {
    private readonly service = inject(TicariBelgeService);
    private readonly authService = inject(AuthService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly messageService = inject(MessageService);
    private readonly route = inject(ActivatedRoute);

    private pendingOpenId: number | null = null;

    belgeler = signal<TicariBelgeDto[]>([]);
    loading = signal(false);
    filter = signal<TicariBelgeFilterDto>(createDefaultTicariBelgeFilter());
    /** p-select tekli seçim için ayrı tutulur; loadBelgeler() sırasında filter().belgeTipleri'ne yansıtılır. */
    selectedBelgeTipi = signal<SatisBelgesiTipi | null>(null);
    /** p-datepicker Date bekler; loadBelgeler() sırasında filter'ın string alanlarına dönüştürülür. */
    baslangicTarihi = signal<Date | null>(null);
    bitisTarihi = signal<Date | null>(null);

    detayDialogVisible = signal(false);
    detayBelge = signal<TicariBelgeDetayDto | null>(null);

    guncelleDialogVisible = signal(false);
    guncelleSaving = signal(false);
    guncelleFormData = signal<TicariBelgeGuncelleRequest | null>(null);
    private guncellenenBelgeId: number | null = null;

    readonly belgeTipiLabels = SATIS_BELGESI_TIPI_LABELS;
    readonly belgeTipiSecenekleri = SATIS_BELGESI_TIPI_SECENEKLERI;
    readonly kaynakModulLabels = SATIS_KAYNAK_MODULU_LABELS;
    readonly ticariDurumLabels = TICARI_BELGE_DURUMU_LABELS;
    readonly ticariDurumSeverities = TICARI_BELGE_DURUMU_SEVERITIES;
    readonly ticariDurumSecenekleri = TICARI_BELGE_DURUM_SECENEKLERI;
    readonly muhasebeDurumuLabels = MUHASEBE_DURUMU_LABELS;
    readonly muhasebeDurumuSeverities = MUHASEBE_DURUMU_SEVERITIES;
    readonly muhasebeDurumuSecenekleri = MUHASEBE_DURUMU_SECENEKLERI;
    readonly faturalamaDurumuLabels = FATURALAMA_DURUMU_LABELS;
    readonly faturalamaDurumuSeverities = FATURALAMA_DURUMU_SEVERITIES;
    readonly faturalamaDurumuSecenekleri = FATURALAMA_DURUMU_SECENEKLERI;

    getMusteriDisplayName = getMusteriDisplayName;

    getBelgeTipiLabel(belgeTipi: SatisBelgesiTipi): string {
        return this.belgeTipiLabels[belgeTipi] ?? String(belgeTipi);
    }

    getKaynakModulLabel(kaynakModul: SatisKaynakModulu): string {
        return this.kaynakModulLabels[kaynakModul] ?? String(kaynakModul);
    }

    getMuhasebeDurumuLabel(durum: TicariBelgeMuhasebeDurumu): string {
        return this.muhasebeDurumuLabels[durum] ?? String(durum);
    }

    getMuhasebeDurumuSeverity(durum: TicariBelgeMuhasebeDurumu): TagSeverity {
        return this.muhasebeDurumuSeverities[durum] ?? 'secondary';
    }

    getFaturalamaDurumuLabel(durum: TicariBelgeFaturalamaDurumu): string {
        return this.faturalamaDurumuLabels[durum] ?? String(durum);
    }

    getFaturalamaDurumuSeverity(durum: TicariBelgeFaturalamaDurumu): TagSeverity {
        return this.faturalamaDurumuSeverities[durum] ?? 'secondary';
    }

    get canView(): boolean {
        return this.authService.hasPermission('TicariBelgeYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('TicariBelgeYonetimi.Manage');
    }

    ngOnInit(): void {
        const idParam = this.route.snapshot.queryParams['id'];
        if (idParam) {
            const parsed = Number(idParam);
            if (!isNaN(parsed) && parsed > 0) {
                this.pendingOpenId = parsed;
            }
        }

        this.loadBelgeler();
    }

    loadBelgeler(): void {
        this.loading.set(true);
        const belgeTipi = this.selectedBelgeTipi();
        const effectiveFilter: TicariBelgeFilterDto = {
            ...this.filter(),
            belgeTipleri: belgeTipi ? [belgeTipi] : null,
            baslangicTarihi: toLocalDateString(this.baslangicTarihi()),
            bitisTarihi: toLocalDateString(this.bitisTarihi())
        };
        this.service
            .filter(effectiveFilter)
            .pipe(finalize(() => this.loading.set(false)))
            .subscribe({
                next: belgeler => {
                    this.belgeler.set(belgeler);
                    this.handlePendingOpen();
                },
                error: err => this.showError(err, 'Belgeler yüklenemedi.')
            });
    }

    clearFilter(): void {
        this.filter.set(createDefaultTicariBelgeFilter());
        this.selectedBelgeTipi.set(null);
        this.baslangicTarihi.set(null);
        this.bitisTarihi.set(null);
        this.loadBelgeler();
    }

    openDetayDialog(belge: TicariBelgeDto): void {
        this.service.getById(belge.id).subscribe({
            next: detay => {
                this.detayBelge.set(detay);
                this.detayDialogVisible.set(true);
            },
            error: err => this.showError(err, 'Belge detayı yüklenemedi.')
        });
    }

    openGuncelleDialog(belge: TicariBelgeDto): void {
        if (!belge.guncellenebilirMi) {
            return;
        }
        this.service.getById(belge.id).subscribe({
            next: detay => {
                this.guncellenenBelgeId = detay.id;
                this.guncelleFormData.set(belgeToGuncelleRequest(detay));
                this.guncelleDialogVisible.set(true);
            },
            error: err => this.showError(err, 'Belge yüklenemedi.')
        });
    }

    saveGuncelle(): void {
        const id = this.guncellenenBelgeId;
        const formData = this.guncelleFormData();
        if (!id || !formData) {
            return;
        }

        this.guncelleSaving.set(true);
        this.service
            .update(id, formData)
            .pipe(finalize(() => this.guncelleSaving.set(false)))
            .subscribe({
                next: () => {
                    this.guncelleDialogVisible.set(false);
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge güncellendi.' });
                    this.loadBelgeler();
                    if (this.detayBelge()?.id === id) {
                        this.refreshDetay(id);
                    }
                },
                error: err => this.showError(err, 'Belge güncellenemedi.')
            });
    }

    confirmDelete(belge: TicariBelgeDto): void {
        if (!belge.silinebilirMi) {
            return;
        }
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" numaralı belgeyi silmek istediğinize emin misiniz?`,
            header: 'Belgeyi Sil',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonProps: { severity: 'danger' },
            accept: () => {
                this.service.delete(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge silindi.' });
                        this.loadBelgeler();
                    },
                    error: err => this.showError(err, 'Belge silinemedi.')
                });
            }
        });
    }

    confirmMuhasebeOnayinaGonder(belge: TicariBelgeDto): void {
        if (!belge.muhasebeOnayinaGonderilebilirMi) {
            return;
        }
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" numaralı belge muhasebe onayına gönderilsin mi?`,
            header: 'Muhasebe Onayına Gönder',
            icon: 'pi pi-send',
            accept: () => {
                this.service.muhasebeOnayinaGonder(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge muhasebe onayına gönderildi.' });
                        this.loadBelgeler();
                        if (this.detayBelge()?.id === belge.id) {
                            this.refreshDetay(belge.id);
                        }
                    },
                    error: err => this.showError(err, 'Belge muhasebe onayına gönderilemedi.')
                });
            }
        });
    }

    confirmIptalEt(belge: TicariBelgeDto): void {
        if (!belge.iptalEdilebilirMi) {
            return;
        }
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" numaralı belgeyi iptal etmek istediğinize emin misiniz?`,
            header: 'Belgeyi İptal Et',
            icon: 'pi pi-exclamation-triangle',
            acceptButtonProps: { severity: 'danger' },
            accept: () => {
                this.service.iptalEt(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge iptal edildi.' });
                        this.loadBelgeler();
                        if (this.detayBelge()?.id === belge.id) {
                            this.refreshDetay(belge.id);
                        }
                    },
                    error: err => this.showError(err, 'Belge iptal edilemedi.')
                });
            }
        });
    }

    private refreshDetay(id: number): void {
        this.service.getById(id).subscribe({ next: detay => this.detayBelge.set(detay) });
    }

    private handlePendingOpen(): void {
        const id = this.pendingOpenId;
        if (!id) {
            return;
        }
        this.pendingOpenId = null;

        const kayit = this.belgeler().find(b => b.id === id);
        if (!kayit) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: `Belge listede bulunamadı. Id: ${id}` });
            return;
        }
        this.openDetayDialog(kayit);
    }

    private showError(err: unknown, fallback: string): void {
        const detail = err instanceof Error && err.message ? err.message : fallback;
        this.messageService.add({ severity: 'error', summary: 'Hata', detail });
    }
}

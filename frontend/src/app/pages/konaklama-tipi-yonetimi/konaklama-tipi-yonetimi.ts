import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KonaklamaTipiIcerikKullanimNoktasiSecenekleri, KonaklamaTipiIcerikKullanimTipiSecenekleri, KonaklamaTipiIcerikPeriyotSecenekleri } from './konaklama-tipi-icerik.constants';
import { KonaklamaTipiTesisAtamaDto, KonaklamaTipiTesisIcerikOverrideDto } from './konaklama-tipi-yonetimi.dto';
import { KonaklamaTipiYonetimiService } from './konaklama-tipi-yonetimi.service';

@Component({
    selector: 'app-konaklama-tipi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './konaklama-tipi-yonetimi.html',
    styles: [`
        .konaklama-page {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .konaklama-section {
            background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
            border: 1px solid #dbe4ee;
            border-radius: 1rem;
            padding: 1.25rem;
            box-shadow: 0 12px 30px rgba(15, 23, 42, 0.04);
        }

        .section-header {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            gap: 1rem;
            margin-bottom: 1rem;
        }

        .section-title {
            margin: 0;
            font-size: 1.1rem;
            font-weight: 700;
            color: #0f172a;
        }

        .section-subtitle {
            margin-top: 0.35rem;
            color: #64748b;
            font-size: 0.92rem;
            line-height: 1.45;
            max-width: 48rem;
        }

        .assignment-controls {
            display: grid;
            grid-template-columns: minmax(18rem, 24rem) minmax(0, 1fr);
            gap: 1rem;
            align-items: end;
            margin-bottom: 1rem;
        }

        .assignment-note {
            display: flex;
            align-items: end;
            min-height: 100%;
        }

        :host ::ng-deep .konaklama-table .p-datatable-wrapper {
            overflow: auto;
        }

        :host ::ng-deep .konaklama-table .p-datatable-table {
            width: 100%;
        }

        :host ::ng-deep .konaklama-table .p-datatable-thead > tr > th {
            white-space: nowrap;
        }

        :host ::ng-deep .konaklama-table .p-datatable-tbody > tr > td {
            vertical-align: top;
        }

        .override-shell {
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }

        .override-note {
            color: #64748b;
            font-size: 0.9rem;
            line-height: 1.4;
        }

        .override-card {
            border: 1px solid #dbe4ee;
            border-radius: 0.85rem;
            padding: 1rem;
            background: #f8fbff;
            display: flex;
            flex-direction: column;
            gap: 0.9rem;
        }

        .override-card.disabled {
            background: #fff7f7;
            border-color: #fecaca;
        }

        .override-card-header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 1rem;
        }

        .override-card-title {
            font-weight: 700;
            color: #0f172a;
        }

        .override-card-subtitle {
            color: #64748b;
            font-size: 0.86rem;
            margin-top: 0.2rem;
        }

        .override-grid {
            display: grid;
            grid-template-columns: repeat(12, minmax(0, 1fr));
            gap: 0.85rem;
        }

        .span-2 { grid-column: span 2; }
        .span-3 { grid-column: span 3; }
        .span-4 { grid-column: span 4; }
        .span-6 { grid-column: span 6; }
        .span-12 { grid-column: span 12; }

        .override-global {
            padding: 0.75rem;
            border-radius: 0.75rem;
            background: #eef6ff;
            color: #334155;
            font-size: 0.88rem;
        }

        .override-checks {
            display: flex;
            gap: 1rem;
            flex-wrap: wrap;
            padding-top: 0.25rem;
        }

        @media (max-width: 991px) {
            .section-header {
                flex-direction: column;
                align-items: stretch;
            }

            .assignment-controls {
                grid-template-columns: 1fr;
            }

            .override-grid {
                grid-template-columns: 1fr;
            }

            .span-2, .span-3, .span-4, .span-6, .span-12 {
                grid-column: span 1;
            }
        }
    `],
    providers: [MessageService]
})
export class KonaklamaTipiYonetimi implements OnInit {
    private readonly service = inject(KonaklamaTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisAtamalari: KonaklamaTipiTesisAtamaDto[] = [];
    selectedTesisId: number | null = null;
    tesisSecenekleri: Array<{ label: string; value: number }> = [];

    loadingBaglam = false;
    loadingAtamalar = false;
    savingAtamalar = false;
    loadingOverride = false;
    savingOverride = false;

    overrideDialogVisible = false;
    selectedOverrideTip: KonaklamaTipiTesisAtamaDto | null = null;
    overrideItems: KonaklamaTipiTesisIcerikOverrideDto[] = [];

    readonly periyotSecenekleri = KonaklamaTipiIcerikPeriyotSecenekleri;
    readonly kullanimTipiSecenekleri = KonaklamaTipiIcerikKullanimTipiSecenekleri;
    readonly kullanimNoktasiSecenekleri = KonaklamaTipiIcerikKullanimNoktasiSecenekleri;

    get canManageAssignments(): boolean {
        return this.hasAnyPermission('KonaklamaTipiTesisAtamaYonetimi.Manage', 'KonaklamaTipiYonetimi.Manage') && !!this.selectedTesisId;
    }

    get canViewGlobalDefinitions(): boolean {
        return this.hasAnyPermission('KonaklamaTipiTanimYonetimi.View', 'KonaklamaTipiYonetimi.View');
    }

    get canManageOverride(): boolean {
        return this.canManageAssignments && this.overrideDialogVisible && !!this.selectedOverrideTip;
    }

    ngOnInit(): void {
        this.loadPageContext();
    }

    refresh(): void {
        this.loadPageContext();
    }

    onTesisChange(): void {
        this.loadTesisAtamalari();
    }

    openGlobalDefinitions(): void {
        void this.router.navigate(['/konaklama-tipi-tanimlari']);
    }

    toggleTesisAtamasi(item: KonaklamaTipiTesisAtamaDto, checked: boolean): void {
        item.tesisteKullanilabilirMi = checked;
    }

    openOverrideDialog(item: KonaklamaTipiTesisAtamaDto): void {
        if (!this.selectedTesisId || !item.tesisteKullanilabilirMi || !item.globalAktifMi) {
            return;
        }

        this.selectedOverrideTip = item;
        this.overrideDialogVisible = true;
        this.overrideItems = [];
        this.loadingOverride = true;

        this.service
            .getTesisIcerikOverride(this.selectedTesisId, item.konaklamaTipiId)
            .pipe(finalize(() => {
                this.loadingOverride = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.overrideItems = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.closeOverrideDialog();
                    this.cdr.detectChanges();
                }
            });
    }

    closeOverrideDialog(): void {
        this.overrideDialogVisible = false;
        this.selectedOverrideTip = null;
        this.overrideItems = [];
        this.loadingOverride = false;
        this.savingOverride = false;
    }

    kaydetOverride(): void {
        if (!this.selectedTesisId || !this.selectedOverrideTip || this.savingOverride) {
            return;
        }

        this.savingOverride = true;
        this.service
            .kaydetTesisIcerikOverride(this.selectedTesisId, this.selectedOverrideTip.konaklamaTipiId, this.overrideItems)
            .pipe(finalize(() => {
                this.savingOverride = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.overrideItems = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Tesis bazli icerik override kaydedildi.' });
                    this.loadTesisAtamalari();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    resetOverride(item: KonaklamaTipiTesisIcerikOverrideDto): void {
        item.devreDisiMi = false;
        item.miktar = item.globalMiktar;
        item.periyot = item.globalPeriyot;
        item.periyotAdi = item.globalPeriyotAdi;
        item.kullanimTipi = item.globalKullanimTipi;
        item.kullanimTipiAdi = item.globalKullanimTipiAdi;
        item.kullanimNoktasi = item.globalKullanimNoktasi;
        item.kullanimNoktasiAdi = item.globalKullanimNoktasiAdi;
        item.kullanimBaslangicSaati = item.globalKullanimBaslangicSaati;
        item.kullanimBitisSaati = item.globalKullanimBitisSaati;
        item.checkInGunuGecerliMi = item.globalCheckInGunuGecerliMi;
        item.checkOutGunuGecerliMi = item.globalCheckOutGunuGecerliMi;
        item.aciklama = item.globalAciklama;
        item.overrideVarMi = false;
    }

    getOverrideOzet(item: KonaklamaTipiTesisIcerikOverrideDto): string {
        if (item.devreDisiMi) {
            return 'Tesiste devre disi';
        }

        return `${item.miktar} x ${item.periyotAdi} • ${item.kullanimNoktasiAdi}`;
    }

    kaydetTesisAtamalari(): void {
        if (!this.canManageAssignments || !this.selectedTesisId || this.savingAtamalar) {
            return;
        }

        const konaklamaTipiIds = this.tesisAtamalari
            .filter((x) => x.tesisteKullanilabilirMi && x.globalAktifMi)
            .map((x) => x.konaklamaTipiId);

        this.savingAtamalar = true;
        this.service
            .kaydetTesisAtamalari(this.selectedTesisId, konaklamaTipiIds)
            .pipe(finalize(() => {
                this.savingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Tesis konaklama tipi atamalari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByKonaklamaTipi(index: number, item: KonaklamaTipiTesisAtamaDto): number {
        return item.konaklamaTipiId;
    }

    trackByOverride(index: number, item: KonaklamaTipiTesisIcerikOverrideDto): number {
        return item.konaklamaTipiIcerikKalemiId;
    }

    private loadPageContext(): void {
        this.loadingBaglam = true;
        this.service
            .getYonetimBaglam()
            .pipe(finalize(() => {
                this.loadingBaglam = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.tesisSecenekleri = baglam.tesisler
                        .map((x) => ({ label: x.ad, value: x.id }))
                        .sort((a, b) => a.label.localeCompare(b.label));

                    if (this.selectedTesisId && !this.tesisSecenekleri.some((x) => x.value === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId) {
                        this.selectedTesisId = this.tesisSecenekleri[0]?.value ?? null;
                    }

                    this.loadTesisAtamalari();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisSecenekleri = [];
                    this.selectedTesisId = null;
                    this.tesisAtamalari = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadTesisAtamalari(): void {
        if (!this.selectedTesisId) {
            this.tesisAtamalari = [];
            return;
        }

        this.loadingAtamalar = true;
        this.service
            .getTesisAtamalari(this.selectedTesisId)
            .pipe(finalize(() => {
                this.loadingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisAtamalari = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private hasAnyPermission(...permissions: string[]): boolean {
        return permissions.some((permission) => this.authService.hasPermission(permission));
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}

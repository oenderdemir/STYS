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
    styleUrl: './konaklama-tipi-yonetimi.scss',
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

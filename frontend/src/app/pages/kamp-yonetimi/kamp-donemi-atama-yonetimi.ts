import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { KampDonemiDto, KampDonemiTesisAtamaDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-donemi-atama-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './kamp-donemi-atama-yonetimi.html',
    styleUrl: './kamp-donemi-atama-yonetimi.scss',
    providers: [MessageService]
})
export class KampDonemiAtamaYonetimi implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    kampDonemleri: KampDonemiDto[] = [];
    selectedSezonKey: string | null = null;
    selectedKampDonemiId: number | null = null;
    topluHedefDonemIds: number[] = [];
    tesisAtamalari: KampDonemiTesisAtamaDto[] = [];

    loadingBaglam = false;
    loadingAtamalar = false;
    savingAtamalar = false;
    savingTopluAtamalar = false;

    get donemSecenekleri(): Array<{ label: string; value: number }> {
        return this.filteredDonemler.map((item) => ({
            label: `${item.yil} - ${item.kampProgramiAd || '-'} / ${item.ad}`,
            value: item.id!
        }));
    }

    get sezonSecenekleri(): Array<{ label: string; value: string }> {
        const unique = new Map<string, { label: string; value: string }>();
        for (const item of this.kampDonemleri) {
            const value = this.buildSezonKey(item.kampProgramiId, item.yil);
            if (!unique.has(value)) {
                unique.set(value, {
                    label: `${item.yil} - ${item.kampProgramiAd || '-'}`,
                    value
                });
            }
        }

        return [...unique.values()].sort((a, b) => b.label.localeCompare(a.label));
    }

    get filteredDonemler(): KampDonemiDto[] {
        if (!this.selectedSezonKey) {
            return this.kampDonemleri;
        }

        return this.kampDonemleri.filter((x) => this.buildSezonKey(x.kampProgramiId, x.yil) === this.selectedSezonKey);
    }

    get canManageAssignments(): boolean {
        return this.canManagePermission && !!this.selectedKampDonemiId;
    }

    get canManagePermission(): boolean {
        return this.hasAnyPermission('KampDonemiTesisAtamaYonetimi.Manage', 'KampDonemiYonetimi.Manage');
    }

    get seciliDonem(): KampDonemiDto | null {
        if (!this.selectedKampDonemiId) {
            return null;
        }

        return this.kampDonemleri.find((x) => x.id === this.selectedKampDonemiId) ?? null;
    }

    get topluAdayDonemSecenekleri(): Array<{ label: string; value: number }> {
        return this.filteredDonemler
            .map((x) => ({
                label: `${x.kampProgramiAd || '-'} / ${x.ad}`,
                value: x.id!
            }));
    }

    get canRunTopluApply(): boolean {
        return this.canManageAssignments && this.topluHedefDonemIds.length > 0 && !this.savingTopluAtamalar && this.tesisAtamalari.length > 0;
    }

    get canViewDonemDefinitions(): boolean {
        return this.hasAnyPermission('KampDonemiTanimYonetimi.View', 'KampDonemiYonetimi.View');
    }

    ngOnInit(): void {
        this.loadPageContext();
    }

    refresh(): void {
        this.loadPageContext();
    }

    onTopluDonemlerChange(): void {
        this.selectedKampDonemiId = this.topluHedefDonemIds[0] ?? null;
        this.loadTesisAtamalari();
    }

    onSezonChange(): void {
        const firstDonemId = this.filteredDonemler[0]?.id ?? null;
        this.topluHedefDonemIds = firstDonemId ? [firstDonemId] : [];
        this.selectedKampDonemiId = firstDonemId;
        this.loadTesisAtamalari();
    }

    openDonemDefinitions(): void {
        void this.router.navigate(['/kamp-donemleri']);
    }

    toggleAtama(item: KampDonemiTesisAtamaDto, checked: boolean): void {
        item.atamaVarMi = checked;
        if (!checked) {
            item.basvuruyaAcikMi = false;
        }
    }

    kaydetTesisAtamalari(): void {
        if (!this.canManageAssignments || !this.selectedKampDonemiId || this.savingAtamalar) {
            return;
        }

        this.savingAtamalar = true;
        this.service
            .kaydetTesisAtamalari(this.selectedKampDonemiId, this.tesisAtamalari)
            .pipe(finalize(() => {
                this.savingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kamp donemi tesis atamalari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    kaydetSeciliDonemlereToplu(): void {
        if (!this.canRunTopluApply) {
            return;
        }

        this.savingTopluAtamalar = true;
        forkJoin(this.topluHedefDonemIds.map((donemId) => this.service.kaydetTesisAtamalari(donemId, this.tesisAtamalari)))
            .pipe(finalize(() => {
                this.savingTopluAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (results) => {
                    const seciliId = this.selectedKampDonemiId;
                    if (seciliId) {
                        const seciliIndex = this.topluHedefDonemIds.findIndex((x) => x === seciliId);
                        if (seciliIndex >= 0) {
                            this.tesisAtamalari = results[seciliIndex];
                        }
                    }

                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `${this.topluHedefDonemIds.length} donem icin tesis atamalari kaydedildi.`
                    });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByTesis(index: number, item: KampDonemiTesisAtamaDto): number {
        return item.tesisId;
    }

    private loadPageContext(): void {
        this.loadingBaglam = true;
        forkJoin({
            donemler: this.service.getKampDonemleri()
        })
            .pipe(finalize(() => {
                this.loadingBaglam = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ donemler }) => {
                    this.kampDonemleri = [...donemler].sort((a, b) => {
                        if (a.yil !== b.yil) {
                            return b.yil - a.yil;
                        }

                        return `${a.kampProgramiAd || ''}${a.ad}`.localeCompare(`${b.kampProgramiAd || ''}${b.ad}`);
                    });

                    if (this.selectedKampDonemiId && !this.kampDonemleri.some((item) => item.id === this.selectedKampDonemiId)) {
                        this.selectedKampDonemiId = null;
                    }

                    if (!this.selectedKampDonemiId) {
                        this.selectedKampDonemiId = this.kampDonemleri.find((item) => item.aktifMi)?.id ?? this.kampDonemleri[0]?.id ?? null;
                    }

                    if (this.selectedKampDonemiId) {
                        const secili = this.kampDonemleri.find((x) => x.id === this.selectedKampDonemiId);
                        if (secili) {
                            this.selectedSezonKey = this.buildSezonKey(secili.kampProgramiId, secili.yil);
                        }
                    } else {
                        this.selectedSezonKey = this.sezonSecenekleri[0]?.value ?? null;
                        this.selectedKampDonemiId = this.filteredDonemler[0]?.id ?? null;
                    }

                    if (this.selectedKampDonemiId) {
                        this.topluHedefDonemIds = [this.selectedKampDonemiId];
                    }

                    this.loadTesisAtamalari();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.kampDonemleri = [];
                    this.selectedKampDonemiId = null;
                    this.tesisAtamalari = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadTesisAtamalari(): void {
        if (!this.selectedKampDonemiId) {
            this.tesisAtamalari = [];
            return;
        }

        this.loadingAtamalar = true;
        this.service
            .getTesisAtamalari(this.selectedKampDonemiId)
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

    private buildSezonKey(kampProgramiId: number, yil: number): string {
        return `${kampProgramiId}-${yil}`;
    }
}

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
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
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
    selectedKampDonemiId: number | null = null;
    tesisAtamalari: KampDonemiTesisAtamaDto[] = [];

    loadingBaglam = false;
    loadingAtamalar = false;
    savingAtamalar = false;

    get donemSecenekleri(): Array<{ label: string; value: number }> {
        return this.kampDonemleri.map((item) => ({
            label: `${item.yil} - ${item.kampProgramiAd || '-'} / ${item.ad}`,
            value: item.id!
        }));
    }

    get canManageAssignments(): boolean {
        return this.hasAnyPermission('KampDonemiTesisAtamaYonetimi.Manage', 'KampDonemiYonetimi.Manage') && !!this.selectedKampDonemiId;
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

    onKampDonemiChange(): void {
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
}

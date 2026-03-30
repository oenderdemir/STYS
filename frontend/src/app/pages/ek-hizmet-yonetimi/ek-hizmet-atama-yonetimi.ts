import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { EkHizmetTesisAtamaDto } from './ek-hizmet-yonetimi.dto';
import { EkHizmetYonetimiService } from './ek-hizmet-yonetimi.service';

@Component({
    selector: 'app-ek-hizmet-atama-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './ek-hizmet-atama-yonetimi.html',
    styles: [`
        .page-shell { display: flex; flex-direction: column; gap: 1.25rem; }
        .page-card { background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%); border: 1px solid #dbe4ee; border-radius: 1rem; padding: 1.25rem; box-shadow: 0 12px 30px rgba(15, 23, 42, 0.04); }
        .page-title { margin: 0; font-size: 1.1rem; font-weight: 700; color: #0f172a; }
        .page-subtitle { margin-top: 0.35rem; color: #64748b; font-size: 0.92rem; line-height: 1.45; }
        .assignment-controls { display: grid; grid-template-columns: minmax(18rem, 24rem) minmax(0, 1fr); gap: 1rem; align-items: end; margin-top: 1rem; }
        .toolbar-links { display: flex; gap: 0.5rem; flex-wrap: wrap; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-wrapper { overflow: auto; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-table { width: 100%; }
        @media (max-width: 991px) { .assignment-controls { grid-template-columns: 1fr; } }
    `],
    providers: [MessageService]
})
export class EkHizmetAtamaYonetimi implements OnInit {
    private readonly service = inject(EkHizmetYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    selectedTesisId: number | null = null;
    tesisSecenekleri: Array<{ label: string; value: number }> = [];
    atamalar: EkHizmetTesisAtamaDto[] = [];

    loadingBaglam = false;
    loadingAtamalar = false;
    saving = false;

    get canManageAssignments(): boolean {
        return this.authService.hasPermission('EkHizmetTesisAtamaYonetimi.Manage') && !!this.selectedTesisId;
    }

    get canViewDefinitions(): boolean {
        return this.authService.hasPermission('EkHizmetTanimYonetimi.View');
    }

    get canViewTariffs(): boolean {
        return this.authService.hasPermission('EkHizmetTarifeYonetimi.View');
    }

    ngOnInit(): void {
        this.loadContext();
    }

    refresh(): void {
        this.loadContext();
    }

    onTesisChange(): void {
        this.loadAtamalar();
    }

    openDefinitions(): void {
        void this.router.navigate(['/ek-hizmet-tanimlari']);
    }

    openTariffs(): void {
        void this.router.navigate(['/ek-hizmet-tarifeleri']);
    }

    toggleAtama(item: EkHizmetTesisAtamaDto, checked: boolean): void {
        item.tesisteKullanilabilirMi = checked;
    }

    kaydet(): void {
        if (!this.canManageAssignments || !this.selectedTesisId || this.saving) {
            return;
        }

        const ids = this.atamalar.filter((x) => x.tesisteKullanilabilirMi && x.globalAktifMi).map((x) => x.globalEkHizmetTanimiId);
        this.saving = true;
        this.service
            .kaydetTesisAtamalari(this.selectedTesisId, ids)
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.atamalar = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Ek hizmet tesis atamalari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByGlobalId(index: number, item: EkHizmetTesisAtamaDto): number {
        return item.globalEkHizmetTanimiId;
    }

    private loadContext(): void {
        this.loadingBaglam = true;
        this.service
            .getTesisler()
            .pipe(finalize(() => {
                this.loadingBaglam = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (tesisler) => {
                    this.tesisSecenekleri = tesisler.map((x) => ({ label: x.ad, value: x.id }));
                    if (this.selectedTesisId && !this.tesisSecenekleri.some((x) => x.value === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId) {
                        this.selectedTesisId = this.tesisSecenekleri[0]?.value ?? null;
                    }

                    this.loadAtamalar();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisSecenekleri = [];
                    this.selectedTesisId = null;
                    this.atamalar = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadAtamalar(): void {
        if (!this.selectedTesisId) {
            this.atamalar = [];
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
                    this.atamalar = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.atamalar = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
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

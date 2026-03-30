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
import { MisafirTipiTesisAtamaDto } from './misafir-tipi-yonetimi.dto';
import { MisafirTipiYonetimiService } from './misafir-tipi-yonetimi.service';

@Component({
    selector: 'app-misafir-tipi-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './misafir-tipi-yonetimi.html',
    styles: [`
        .misafir-page {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }

        .misafir-section {
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

        :host ::ng-deep .misafir-table .p-datatable-wrapper {
            overflow: auto;
        }

        :host ::ng-deep .misafir-table .p-datatable-table {
            width: 100%;
        }

        :host ::ng-deep .misafir-table .p-datatable-thead > tr > th {
            white-space: nowrap;
        }

        :host ::ng-deep .misafir-table .p-datatable-tbody > tr > td {
            vertical-align: top;
        }

        @media (max-width: 991px) {
            .section-header {
                flex-direction: column;
                align-items: stretch;
            }

            .assignment-controls {
                grid-template-columns: 1fr;
            }
        }
    `],
    providers: [MessageService]
})
export class MisafirTipiYonetimi implements OnInit {
    private readonly service = inject(MisafirTipiYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisAtamalari: MisafirTipiTesisAtamaDto[] = [];
    selectedTesisId: number | null = null;
    tesisSecenekleri: Array<{ label: string; value: number }> = [];

    loadingBaglam = false;
    loadingAtamalar = false;
    savingAtamalar = false;

    get canManageAssignments(): boolean {
        return this.hasAnyPermission('MisafirTipiTesisAtamaYonetimi.Manage', 'MisafirTipiYonetimi.Manage') && !!this.selectedTesisId;
    }

    get canViewGlobalDefinitions(): boolean {
        return this.hasAnyPermission('MisafirTipiTanimYonetimi.View', 'MisafirTipiYonetimi.View');
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
        void this.router.navigate(['/misafir-tipi-tanimlari']);
    }

    toggleTesisAtamasi(item: MisafirTipiTesisAtamaDto, checked: boolean): void {
        item.tesisteKullanilabilirMi = checked;
    }

    kaydetTesisAtamalari(): void {
        if (!this.canManageAssignments || !this.selectedTesisId || this.savingAtamalar) {
            return;
        }

        const misafirTipiIds = this.tesisAtamalari
            .filter((x) => x.tesisteKullanilabilirMi && x.globalAktifMi)
            .map((x) => x.misafirTipiId);

        this.savingAtamalar = true;
        this.service
            .kaydetTesisAtamalari(this.selectedTesisId, misafirTipiIds)
            .pipe(finalize(() => {
                this.savingAtamalar = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.tesisAtamalari = items;
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Tesis misafir tipi atamalari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByMisafirTipi(index: number, item: MisafirTipiTesisAtamaDto): number {
        return item.misafirTipiId;
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

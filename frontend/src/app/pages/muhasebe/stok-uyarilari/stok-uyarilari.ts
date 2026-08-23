import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { DepolarService } from '../depolar/depolar.service';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { StokUyariModel } from './stok-uyarilari.dto';
import { StokUyarilariService } from './stok-uyarilari.service';

@Component({
    selector: 'app-stok-uyarilari-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './stok-uyarilari.html',
    providers: [MessageService]
})
export class StokUyarilariPage implements OnInit {
    private readonly service = inject(StokUyarilariService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartlariService = inject(TasinirKartlariService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    records: StokUyariModel[] = [];
    depoOptions: Array<{ label: string; value: number }> = [];
    tasinirKartOptions: Array<{ label: string; value: number }> = [];
    selectedDepoId?: number;
    selectedTasinirKartId?: number;
    sadeceRiskli = true;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.selectedDepoId = undefined;
        this.selectedTasinirKartId = undefined;
        this.loadReferences();
        this.load();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    loadReferences(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.depolarService.getAll(tesisId).subscribe({
            next: (items) => {
                this.depoOptions = items.filter((x) => x.aktifMi).map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });

        this.tasinirKartlariService.getAll(tesisId).subscribe({
            next: (items) => {
                this.tasinirKartOptions = items.filter((x) => x.aktifMi).map((x) => ({ label: `${x.stokKodu} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            }
        });
    }

    load(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getAll(tesisId, this.selectedDepoId, this.selectedTasinirKartId, this.sadeceRiskli)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.records = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
    }

    clearFilters(): void {
        this.selectedDepoId = undefined;
        this.selectedTasinirKartId = undefined;
        this.sadeceRiskli = true;
        this.load();
    }

    getDurumSeverity(durum: string): 'danger' | 'warn' | 'success' | 'secondary' {
        switch (durum) {
            case 'Kritik':
                return 'danger';
            case 'Dusuk':
                return 'warn';
            case 'Normal':
                return 'success';
            default:
                return 'secondary';
        }
    }

    private showError(error: unknown): void {
        const detail = error instanceof HttpErrorResponse
            ? error.error?.message ?? error.message
            : error instanceof Error
                ? error.message
                : 'İşlem tamamlanamadı.';

        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail });
        this.cdr.detectChanges();
    }
}


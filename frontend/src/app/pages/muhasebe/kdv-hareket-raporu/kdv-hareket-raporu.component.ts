import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DatePickerModule } from 'primeng/datepicker';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { KdvHareketRaporuService } from '../services/kdv-hareket-raporu.service';
import {
    BELGE_YONU_SECENEKLERI,
    KdvHareketRaporModel,
    KdvRaporFilterModel,
    TevkifatHareketRaporModel,
    createDefaultKdvRaporFilter
} from '../models/kdv-hareket-raporu.model';

@Component({
    selector: 'app-kdv-hareket-raporu',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ToastModule,
        ButtonModule,
        CardModule,
        TableModule,
        TagModule,
        MessageModule,
        SelectModule,
        CheckboxModule,
        ProgressSpinnerModule,
        DatePickerModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    providers: [MessageService],
    templateUrl: './kdv-hareket-raporu.component.html',
    styleUrl: './kdv-hareket-raporu.component.scss'
})
export class KdvHareketRaporuComponent implements OnInit {
    private readonly raporService = inject(KdvHareketRaporuService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: KdvRaporFilterModel = createDefaultKdvRaporFilter();
    kdvRapor: KdvHareketRaporModel | null = null;
    tevkifatRapor: TevkifatHareketRaporModel | null = null;
    loading = false;
    loadingMessage = 'KDV hareket raporu yükleniyor...';
    belgeYonuSecenekleri = BELGE_YONU_SECENEKLERI;

    private contextInitialized = false;
    private currentTesisId: number | null = null;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.filter.tesisId = tesisId;
        this.clearResults();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.filter.tesisId = this.currentTesisId;
                this.cdr.markForCheck();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    clearFilter(): void {
        this.filter = createDefaultKdvRaporFilter();
        this.filter.tesisId = this.currentTesisId;
        this.clearResults();
    }

    loadRapor(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        this.loading = true;
        this.clearResults();

        forkJoin({
            kdv: this.raporService.getRapor(this.filter),
            tevkifat: this.raporService.getTevkifatRapor(this.filter)
        }).pipe(
            finalize(() => {
                this.loading = false;
                this.cdr.markForCheck();
            })
        ).subscribe({
            next: ({ kdv, tevkifat }) => {
                this.kdvRapor = kdv;
                this.tevkifatRapor = tevkifat;
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    formatPara(value: number): string {
        return new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
    }

    formatTarih(value: string): string {
        if (!value) return '';
        return new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value));
    }

    formatOran(value: number): string {
        return `%${new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(value)}`;
    }

    getBelgeTipiLabel(value: string): string {
        switch (value) {
            case 'FaturaTaslagi': return 'Fatura Taslağı';
            case 'SatisFaturasi': return 'Satış Faturası';
            case 'AlisFaturasi': return 'Alış Faturası';
            case 'SatisIadeFaturasi': return 'Satış İade Faturası';
            case 'AlisIadeFaturasi': return 'Alış İade Faturası';
            case 'IadeFaturasi': return 'Legacy İade';
            case 'Proforma': return 'Proforma';
            default: return value;
        }
    }

    getBelgeYonuLabel(value: string): string {
        switch (value) {
            case 'Satis': return 'Satış';
            case 'Alis': return 'Alış';
            case 'Iade': return 'İade';
            default: return value;
        }
    }

    getBelgeYonuSeverity(value: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (value) {
            case 'Satis': return 'success';
            case 'Alis': return 'danger';
            case 'Iade': return 'warn';
            default: return 'secondary';
        }
    }

    getKdvTipiSeverity(value: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (value) {
            case 'KDV\'li': return 'info';
            case 'Tam İstisna': return 'success';
            case 'Kısmi İstisna': return 'warn';
            case 'KDV Kapsam Dışı': return 'secondary';
            case 'Tevkifatlı': return 'danger';
            default: return 'secondary';
        }
    }

    private clearResults(): void {
        this.kdvRapor = null;
        this.tevkifatRapor = null;
    }

    private tryGetSeciliTesisId(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: 'warn',
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe raporunu çalıştırmak için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    private showError(error: unknown): void {
        let detail = 'Beklenmeyen bir hata oluştu.';
        if (error instanceof HttpErrorResponse) {
            if (typeof error.error === 'object' && error.error?.detail) {
                detail = error.error.detail;
            } else if (error.error?.message) {
                detail = error.error.message;
            } else if (error.status === 403) {
                detail = 'Bu işlem için yetkiniz bulunmamaktadır.';
            }
        } else if (error instanceof Error) {
            detail = error.message;
        }
        this.messageService.add({
            severity: 'error',
            summary: 'Hata',
            detail
        });
    }
}

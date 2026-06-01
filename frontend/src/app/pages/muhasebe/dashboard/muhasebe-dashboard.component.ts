import { CommonModule } from '@angular/common';
import { Component, OnInit, effect, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SkeletonModule } from 'primeng/skeleton';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { MuhasebeDashboardService } from '../services/muhasebe-dashboard.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    MuhasebeDashboardFilterModel,
    MuhasebeDashboardModel,
    createDefaultDashboardFilter
} from '../models/muhasebe-dashboard.model';
import { Toolbar } from "primeng/toolbar";

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const suankiYil = new Date().getFullYear();
    const yillar: Array<{ label: string; value: number | null }> = [];
    for (let y = suankiYil + 1; y >= suankiYil - 4; y--) {
        yillar.push({ label: String(y), value: y });
    }
    return yillar;
})();

@Component({
    selector: 'app-muhasebe-dashboard',
    standalone: true,
    imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    ToastModule,
    ButtonModule,
    CardModule,
    TableModule,
    TagModule,
    MessageModule,
    SelectModule,
    ProgressSpinnerModule,
    SkeletonModule,
    MuhasebeTesisSecimDialogComponent,
    MuhasebeTesisContextBarComponent,
    Toolbar
],
    providers: [MessageService],
    templateUrl: './muhasebe-dashboard.component.html',
    styleUrl: './muhasebe-dashboard.component.scss'
})
export class MuhasebeDashboardComponent implements OnInit {
    private readonly dashboardService = inject(MuhasebeDashboardService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);

    filter: MuhasebeDashboardFilterModel = createDefaultDashboardFilter();
    dashboard: MuhasebeDashboardModel | null = null;
    loading = false;
    loadingMessage = 'Dashboard yükleniyor...';

    maliYilSecenekleri = MALI_YIL_SECENEKLERI;

    donemSecenekleri: Array<{ label: string; value: number | null }> = [
        { label: 'Tümü', value: null },
        ...Array.from({ length: 12 }, (_, i) => ({ label: String(i + 1), value: i + 1 }))
    ];
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        this.filter.tesisId = tesisId;
        this.dashboard = null;
        this.loadDashboard();
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.filter.tesisId = this.currentTesisId;
                this.loadDashboard();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    loadDashboard(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            this.dashboard = null;
            return;
        }

        this.filter.tesisId = tesisId;
        this.loading = true;
        this.dashboard = null;
        this.dashboardService.getDashboard(this.filter).pipe(
            finalize(() => (this.loading = false))
        ).subscribe({
            next: (data) => {
                this.dashboard = data;
            },
            error: (error: unknown) => {
                this.showError(error);
                this.dashboard = null;
            }
        });
    }

    onFilterChange(): void {
        this.loadDashboard();
    }

    // Yardımcı metotlar
    formatTarih(tarih: string): string {
        if (!tarih) return '';
        const date = new Date(tarih);
        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
        }).format(date);
    }

    formatTarihKisa(tarih: string): string {
        if (!tarih) return '';
        const date = new Date(tarih);
        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: 'short'
        }).format(date);
    }

    formatPara(deger: number): string {
        return new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(deger);
    }

    getDurumLabel(durum: string): string {
        switch (durum) {
            case 'Taslak': return 'Taslak';
            case 'Onayli': return 'Onaylı';
            case 'Iptal': return 'İptal';
            case 'TersKayit': return 'Ters Kayıt';
            default: return durum;
        }
    }

    getDurumSeverity(durum: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (durum) {
            case 'Onayli': return 'success';
            case 'Taslak': return 'warn';
            case 'Iptal': return 'danger';
            case 'TersKayit': return 'info';
            default: return 'secondary';
        }
    }

    getFisTipiLabel(fisTipi: string): string {
        switch (fisTipi) {
            case 'Mahsup': return 'Mahsup';
            case 'Tahsil': return 'Tahsil';
            case 'Tediye': return 'Tediye';
            case 'Tasinir': return 'Taşınır';
            case 'Amortisman': return 'Amortisman';
            default: return fisTipi;
        }
    }

    getFisTipiSeverity(fisTipi: string): 'info' | 'secondary' {
        switch (fisTipi) {
            case 'Mahsup': return 'info';
            case 'Tahsil': return 'info';
            case 'Tediye': return 'info';
            default: return 'secondary';
        }
    }

    getUyariSeverity(severity: string): 'warn' | 'info' | 'error' | 'success' | 'secondary' {
        switch (severity) {
            case 'warn': return 'warn';
            case 'info': return 'info';
            case 'danger': return 'error';
            case 'error': return 'error';
            default: return 'info';
        }
    }

    getDonemKapatSeverity(kapaliMi: boolean): 'success' | 'warn' {
        return kapaliMi ? 'success' : 'warn';
    }

    getDonemKapatLabel(kapaliMi: boolean): string {
        return kapaliMi ? 'Kapalı' : 'Açık';
    }

    getFarkSinifi(fark: number): string {
        if (Math.abs(fark) < 0.01) return 'text-green-600 font-semibold';
        return 'text-red-600 font-semibold';
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
        let mesaj = 'Beklenmeyen bir hata oluştu.';
        if (error instanceof HttpErrorResponse) {
            if (typeof error.error === 'string') {
                mesaj = error.error;
            } else if (error.error?.message) {
                mesaj = error.error.message;
            } else if (error.message) {
                mesaj = error.message;
            }
        } else if (error instanceof Error) {
            mesaj = error.message;
        }
        this.messageService.add({
            severity: 'error',
            summary: 'Hata',
            detail: mesaj,
            life: 6000
        });
    }
}

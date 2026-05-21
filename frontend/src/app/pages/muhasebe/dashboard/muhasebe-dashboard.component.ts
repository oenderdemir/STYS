import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
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
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';
import {
    MuhasebeDashboardFilterModel,
    MuhasebeDashboardModel,
    MuhasebeDashboardDonemOzetModel,
    MuhasebeDashboardFisOzetModel,
    MuhasebeDashboardUyariModel,
    createDefaultDashboardFilter
} from '../models/muhasebe-dashboard.model';

interface TesisSecenek {
    label: string;
    value: number | null;
}

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
        SkeletonModule
    ],
    providers: [MessageService],
    templateUrl: './muhasebe-dashboard.component.html',
    styleUrl: './muhasebe-dashboard.component.scss'
})
export class MuhasebeDashboardComponent implements OnInit {
    private readonly dashboardService = inject(MuhasebeDashboardService);
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly messageService = inject(MessageService);

    filter: MuhasebeDashboardFilterModel = createDefaultDashboardFilter();
    dashboard: MuhasebeDashboardModel | null = null;
    loading = false;
    loadingMessage = 'Dashboard yükleniyor...';

    tesisSecenekleri: TesisSecenek[] = [];
    tesisLoading = false;

    maliYilSecenekleri = MALI_YIL_SECENEKLERI;

    ngOnInit(): void {
        this.loadTesisler();
        this.loadDashboard();
    }

    private loadTesisler(): void {
        this.tesisLoading = true;
        this.raporService.getTesisler().pipe(
            finalize(() => (this.tesisLoading = false))
        ).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler.map(t => ({
                    label: t.ad ?? `Tesis #${t.id}`,
                    value: t.id
                }));
            },
            error: (error: unknown) => {
                this.showError(error);
                this.tesisSecenekleri = [];
            }
        });
    }

    loadDashboard(): void {
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

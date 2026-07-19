import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { FormsModule } from '@angular/forms';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { DatePickerModule } from 'primeng/datepicker';
import { TooltipModule } from 'primeng/tooltip';
import { HttpErrorResponse } from '@angular/common/http';
import { KdvBeyannameHazirlikKontrolService } from '../services/kdv-beyanname-hazirlik-kontrol.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { DepolarService } from '../depolar/depolar.service';
import {
    KdvBeyannameHazirlikKontrolFilterModel,
    KdvBeyannameHazirlikKontrolModel,
    KdvBeyannameHazirlikKontrolMaddesiModel,
    createDefaultKdvBeyannameHazirlikKontrolFilter,
    DONEM_SECENEKLERI,
    getMaliYilSecenekleri
} from '../models/kdv-beyanname-hazirlik-kontrol.model';

interface Secenek<T = number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-kdv-beyanname-hazirlik-kontrol',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        ToastModule,
        ButtonModule,
        CardModule,
        TagModule,
        MessageModule,
        SelectModule,
        ProgressSpinnerModule,
        DatePickerModule,
        TooltipModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    providers: [MessageService],
    templateUrl: './kdv-beyanname-hazirlik-kontrol.component.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    styleUrl: './kdv-beyanname-hazirlik-kontrol.component.scss'
})
export class KdvBeyannameHazirlikKontrolComponent implements OnInit {
    private readonly kontrolService = inject(KdvBeyannameHazirlikKontrolService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly depolarService = inject(DepolarService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: KdvBeyannameHazirlikKontrolFilterModel = createDefaultKdvBeyannameHazirlikKontrolFilter();
    result: KdvBeyannameHazirlikKontrolModel | null = null;
    loading = false;
    loadingMessage = 'KDV beyanname hazırlık kontrolü yapılıyor...';

    maliYilSecenekleri: Secenek<number>[] = getMaliYilSecenekleri();
    donemSecenekleri = DONEM_SECENEKLERI;

    depoSecenekleri: Secenek<number>[] = [];
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
            error: (error: unknown) => {
                this.showError(error);
            }
        });
        this.loadDepolar();
    }

    private loadDepolar(): void {
        this.depolarService.getAll().subscribe({
            next: (depolar) => {
                this.depoSecenekleri = depolar.map((d) => ({
                    label: d.ad ?? d.kod ?? `Depo #${d.id}`,
                    value: d.id!
                }));
            },
            error: () => {
                this.depoSecenekleri = [];
            }
        });
    }

    clearFilter(): void {
        this.filter = createDefaultKdvBeyannameHazirlikKontrolFilter();
        this.filter.tesisId = this.currentTesisId;
        this.clearResults();
    }

    runKontrol(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        this.loading = true;
        this.clearResults();
        this.kontrolService
            .kontrolEt(this.filter)
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (data) => {
                    this.result = data;
                },
                error: (error: unknown) => {
                    this.showError(error);
                    this.result = null;
                }
            });
    }

    onFilterChange(): void {
        // Don't auto-load on every change; user clicks "Kontrol Et" button
    }

    formatPara(deger: number): string {
        return new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(deger);
    }

    getDurumSeverity(durum: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' | 'contrast' {
        switch (durum) {
            case 'Basarili':
                return 'success';
            case 'Uyari':
                return 'warn';
            case 'Bloklayici':
                return 'danger';
            default:
                return 'info';
        }
    }

    getDurumIcon(durum: string): string {
        switch (durum) {
            case 'Basarili':
                return 'pi pi-check-circle';
            case 'Uyari':
                return 'pi pi-exclamation-triangle';
            case 'Bloklayici':
                return 'pi pi-times-circle';
            default:
                return 'pi pi-info-circle';
        }
    }

    netKdvClass(netKdv: number): string {
        if (netKdv > 0) return 'text-green-500';
        if (netKdv < 0) return 'text-red-500';
        return '';
    }

    getProgressPercent(): number {
        if (!this.result || this.result.toplamKontrolSayisi === 0) return 0;
        return Math.round((this.result.basariliKontrolSayisi / this.result.toplamKontrolSayisi) * 100);
    }

    getProgressColor(): string {
        const pct = this.getProgressPercent();
        if (pct >= 100) return '#22c55e';
        if (pct >= 70) return '#eab308';
        return '#ef4444';
    }

    private clearResults(): void {
        this.result = null;
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

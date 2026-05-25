import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
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
import { DatePickerModule } from 'primeng/datepicker';
import { HttpErrorResponse } from '@angular/common/http';
import { KdvOzetRaporuService } from '../services/kdv-ozet-raporu.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { DepolarService } from '../depolar/depolar.service';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import { KdvIstisnaTanimDto, createDefaultKdvIstisnaTanimFilter } from '../models/kdv-istisna-tanim.model';
import { DepoModel } from '../depolar/depolar.dto';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { STOK_HAREKET_TIPLERI } from '../stok-hareketleri/stok-hareketleri.dto';
import {
    KdvOzetRaporFilterModel,
    KdvOzetRaporModel,
    KdvOzetRaporOzetModel,
    KdvUygulamaTipiOzetModel,
    KdvIstisnaKoduOzetModel,
    KdvOzetRaporUyariModel,
    createDefaultKdvOzetRaporFilter,
    DONEM_SECENEKLERI,
    getMaliYilSecenekleri,
    getDonemLabel,
    UYARI_KODU_LABELLERI,
    UYARI_KODU_ICONS
} from '../models/kdv-ozet-raporu.model';
import { KDV_UYGULAMA_TIPI_SECENEKLERI, MUS_FIS_DURUMU_SECENEKLERI } from '../models/kdv-hareket-raporu.model';

interface Secenek<T = number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-kdv-ozet-raporu',
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
        DatePickerModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    providers: [MessageService],
    templateUrl: './kdv-ozet-raporu.component.html',
    styleUrl: './kdv-ozet-raporu.component.scss'
})
export class KdvOzetRaporuComponent implements OnInit {
    private readonly raporService = inject(KdvOzetRaporuService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: KdvOzetRaporFilterModel = createDefaultKdvOzetRaporFilter();
    rapor: KdvOzetRaporModel | null = null;
    loading = false;
    exporting = false;
    loadingMessage = 'KDV özet raporu yükleniyor...';

    maliYilSecenekleri: Secenek<number>[] = getMaliYilSecenekleri();
    donemSecenekleri = DONEM_SECENEKLERI;

    depoSecenekleri: Secenek<number>[] = [];
    tasinirKartSecenekleri: Secenek<number>[] = [];
    hareketTipiSecenekleri = STOK_HAREKET_TIPLERI;
    istisnaTanimSecenekleri: Secenek<number>[] = [];

    kdvUygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI;
    musFisDurumuSecenekleri = MUS_FIS_DURUMU_SECENEKLERI;
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
        this.loadTasinirKartlar();
        this.loadIstisnaTanimlari();
    }

    private loadDepolar(): void {
        this.depolarService.getAll().subscribe({
            next: (depolar) => {
                this.depoSecenekleri = depolar.map(d => ({
                    label: d.ad ?? d.kod ?? `Depo #${d.id}`,
                    value: d.id!
                }));
            },
            error: () => {
                this.depoSecenekleri = [];
            }
        });
    }

    private loadTasinirKartlar(): void {
        this.tasinirKartService.getAll().subscribe({
            next: (kartlar) => {
                this.tasinirKartSecenekleri = kartlar.map(k => ({
                    label: `${k.stokKodu} - ${k.ad}`,
                    value: k.id!
                }));
            },
            error: () => {
                this.tasinirKartSecenekleri = [];
            }
        });
    }

    private loadIstisnaTanimlari(): void {
        this.kdvIstisnaTanimService.filter(createDefaultKdvIstisnaTanimFilter()).subscribe({
            next: (tanimlar) => {
                this.istisnaTanimSecenekleri = tanimlar.map(t => ({
                    label: `${t.kod} - ${t.ad}`,
                    value: t.id
                }));
            },
            error: () => {
                this.istisnaTanimSecenekleri = [];
            }
        });
    }

    clearFilter(): void {
        this.filter = createDefaultKdvOzetRaporFilter();
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
        this.raporService.getOzetRapor(this.filter).pipe(
            finalize(() => {
                this.loading = false;
                this.cdr.markForCheck();
            })
        ).subscribe({
            next: (data) => {
                this.rapor = data;
            },
            error: (error: unknown) => {
                this.showError(error);
                this.rapor = null;
            }
        });
    }

    exportExcel(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        this.exporting = true;
        this.cdr.markForCheck();

        this.raporService.exportExcel(this.filter).pipe(
            finalize(() => {
                this.exporting = false;
                this.cdr.markForCheck();
            })
        ).subscribe({
            next: (blob) => {
                this.downloadBlob(blob, this.getExportFileName());
                this.messageService.add({
                    severity: 'success',
                    summary: 'Başarılı',
                    detail: 'KDV özet raporu Excel olarak indirildi.'
                });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private getExportFileName(): string {
        const now = new Date();
        const pad = (n: number) => n.toString().padStart(2, '0');
        const timestamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
        return `kdv-ozet-raporu-${timestamp}.xlsx`;
    }

    private downloadBlob(blob: Blob, fileName: string): void {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        window.URL.revokeObjectURL(url);
    }

    onFilterChange(): void {
        // Don't auto-load on every change; user clicks "Sorgula" button
    }

    formatPara(deger: number): string {
        return new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(deger);
    }

    formatTarih(tarih: string): string {
        if (!tarih) return '';
        const date = new Date(tarih);
        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
        }).format(date);
    }

    getKdvTipiSeverity(kdvUygulamaTipi: number): 'success' | 'warn' | 'danger' | 'info' | 'secondary' | 'contrast' {
        switch (kdvUygulamaTipi) {
            case 1: return 'info';
            case 2: return 'success';
            case 3: return 'info';
            case 4: return 'secondary';
            case 5: return 'danger';
            default: return 'info';
        }
    }

    getUyariSeverity(severity: string | null | undefined): 'warn' | 'error' | 'info' | 'success' | 'secondary' | 'contrast' {
        if (!severity) return 'info';
        switch (severity) {
            case 'warn': return 'warn';
            case 'error': return 'error';
            case 'info': return 'info';
            case 'success': return 'success';
            case 'secondary': return 'secondary';
            case 'contrast': return 'contrast';
            // Backward compatibility: map by uyari kodu
            case 'MUHASEBE_FISI_EKSIK': return 'warn';
            case 'KDV_TUTARI_EKSIK': return 'error';
            case 'ISTISNA_KODU_EKSIK': return 'warn';
            case 'TEVKIFATLI_HAREKET_VAR': return 'info';
            default: return 'info';
        }
    }

    getUyariIcon(uyariKodu: string): string {
        return UYARI_KODU_ICONS[uyariKodu] ?? 'pi pi-info-circle';
    }

    getUyariLabel(uyariKodu: string): string {
        return UYARI_KODU_LABELLERI[uyariKodu] ?? uyariKodu;
    }

    netKdvClass(netKdv: number): string {
        if (netKdv > 0) return 'text-green-500';
        if (netKdv < 0) return 'text-red-500';
        return '';
    }

    private clearResults(): void {
        this.rapor = null;
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

import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
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
import { TooltipModule } from 'primeng/tooltip';
import { HttpErrorResponse } from '@angular/common/http';
import { KdvHareketRaporuService } from '../services/kdv-hareket-raporu.service';
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
    KdvHareketRaporFilterModel,
    KdvHareketRaporModel,
    KdvHareketRaporOzetModel,
    KdvHareketRaporSatirModel,
    createDefaultKdvHareketRaporFilter,
    KDV_UYGULAMA_TIPI_SECENEKLERI,
    MUS_FIS_DURUMU_SECENEKLERI
} from '../models/kdv-hareket-raporu.model';

interface Secenek<T = number> {
    label: string;
    value: T;
}

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
        ProgressSpinnerModule,
        DatePickerModule,
        TooltipModule,
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
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: KdvHareketRaporFilterModel = createDefaultKdvHareketRaporFilter();
    rapor: KdvHareketRaporModel | null = null;
    loading = false;
    exporting = false;
    loadingMessage = 'KDV hareket raporu yükleniyor...';

    depoSecenekleri: Secenek<number>[] = [];
    tasinirKartSecenekleri: Secenek<number>[] = [];
    hareketTipiSecenekleri = STOK_HAREKET_TIPLERI;
    istisnaTanimSecenekleri: Secenek<number>[] = [];

    kdvUygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI;
    musFisDurumuSecenekleri = MUS_FIS_DURUMU_SECENEKLERI;

    // Toplu fiş oluşturma
    selectedRows: KdvHareketRaporSatirModel[] = [];
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
                this.cdr.detectChanges();
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
        this.filter = createDefaultKdvHareketRaporFilter();
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
        this.raporService.getRapor(this.filter).pipe(
            finalize(() => (this.loading = false))
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

    onFilterChange(): void {
        this.loadRapor();
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

    formatTarihSaat(tarih: string): string {
        if (!tarih) return '';
        const date = new Date(tarih);
        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        }).format(date);
    }

    formatPara(deger: number): string {
        return new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(deger);
    }

    formatOran(orani: number): string {
        return `%${new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2
        }).format(orani)}`;
    }

    getHareketTipiLabel(tip: string): string {
        switch (tip) {
            case 'Giris': return 'Giriş';
            case 'Cikis': return 'Çıkış';
            case 'Transfer': return 'Transfer';
            case 'Iade': return 'İade';
            case 'Sarf': return 'Sarf';
            case 'SayimFarki': return 'Sayım Farkı';
            case 'Zimmet': return 'Zimmet';
            default: return tip;
        }
    }

    getHareketTipiSeverity(tip: string): 'success' | 'danger' | 'info' | 'warn' | 'secondary' {
        switch (tip) {
            case 'Giris':
            case 'Iade':
                return 'success';
            case 'Cikis':
            case 'Sarf':
            case 'Zimmet':
                return 'danger';
            case 'Transfer':
            case 'SayimFarki':
                return 'info';
            default: return 'secondary';
        }
    }

    getKdvTipiSeverity(tip: number): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
        switch (tip) {
            case 1: return 'info';       // KDV'li
            case 2: return 'warn';       // Tam İstisna
            case 3: return 'warn';       // Kısmi İstisna
            case 4: return 'secondary';  // KDV Kapsam Dışı
            case 5: return 'danger';     // Tevkifatlı
            default: return 'secondary';
        }
    }

    getMusFisDurumuSeverity(durum: string | null | undefined): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        if (!durum) return 'secondary';
        switch (durum) {
            case 'Onayli': return 'success';
            case 'Taslak': return 'warn';
            case 'Iptal': return 'danger';
            case 'TersKayit': return 'info';
            default: return 'secondary';
        }
    }

    getMusFisDurumuLabel(durum: string | null | undefined): string {
        if (!durum) return '—';
        switch (durum) {
            case 'Taslak': return 'Taslak';
            case 'Onayli': return 'Onaylı';
            case 'Iptal': return 'İptal';
            case 'TersKayit': return 'Ters Kayıt';
            default: return durum;
        }
    }

    exportExcel(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        this.exporting = true;
        this.cdr.detectChanges();
        this.raporService.exportExcel(this.filter).pipe(
            finalize(() => {
                this.exporting = false;
                this.cdr.detectChanges();
            })
        ).subscribe({
            next: (blob) => {
                this.downloadBlob(blob, this.getTimestamp());
                this.messageService.add({
                    severity: 'success',
                    summary: 'Başarılı',
                    detail: 'KDV hareket raporu Excel olarak indirildi.',
                    life: 4000
                });
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private downloadBlob(blob: Blob, fileName: string): void {
        const url = window.URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        window.URL.revokeObjectURL(url);
        anchor.remove();
    }

    private getTimestamp(): string {
        const now = new Date();
        const pad = (n: number) => n.toString().padStart(2, '0');
        return `kdv-hareket-raporu-${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}.xlsx`;
    }

    navigateToFis(satir: KdvHareketRaporSatirModel): void {
        if (!satir.musFisId) return;
        this.router.navigate(['/muhasebe/fisler'], {
            queryParams: {
                id: satir.musFisId,
                fisNo: satir.musFisNo
            }
        });
    }

    private clearResults(): void {
        this.rapor = null;
        this.selectedRows = [];
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

import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
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
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';
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

interface TesisSecenek {
    label: string;
    value: number | null;
}

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
        DatePickerModule
    ],
    providers: [MessageService],
    templateUrl: './kdv-ozet-raporu.component.html',
    styleUrl: './kdv-ozet-raporu.component.scss'
})
export class KdvOzetRaporuComponent implements OnInit {
    private readonly raporService = inject(KdvOzetRaporuService);
    private readonly muhasebeRaporService = inject(MuhasebeRaporService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: KdvOzetRaporFilterModel = createDefaultKdvOzetRaporFilter();
    rapor: KdvOzetRaporModel | null = null;
    loading = false;
    loadingMessage = 'KDV özet raporu yükleniyor...';

    maliYilSecenekleri: Secenek<number>[] = getMaliYilSecenekleri();
    donemSecenekleri = DONEM_SECENEKLERI;

    tesisSecenekleri: TesisSecenek[] = [];
    tesisLoading = false;

    depoSecenekleri: Secenek<number>[] = [];
    tasinirKartSecenekleri: Secenek<number>[] = [];
    hareketTipiSecenekleri = STOK_HAREKET_TIPLERI;
    istisnaTanimSecenekleri: Secenek<number>[] = [];

    kdvUygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI;
    musFisDurumuSecenekleri = MUS_FIS_DURUMU_SECENEKLERI;

    ngOnInit(): void {
        this.loadTesisler();
        this.loadDepolar();
        this.loadTasinirKartlar();
        this.loadIstisnaTanimlari();
    }

    private loadTesisler(): void {
        this.tesisLoading = true;
        this.muhasebeRaporService.getTesisler().pipe(
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
        this.rapor = null;
    }

    loadRapor(): void {
        this.loading = true;
        this.rapor = null;
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

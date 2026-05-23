import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
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
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';
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

interface TesisSecenek {
    label: string;
    value: number | null;
}

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
        TooltipModule
    ],
    providers: [MessageService],
    templateUrl: './kdv-hareket-raporu.component.html',
    styleUrl: './kdv-hareket-raporu.component.scss'
})
export class KdvHareketRaporuComponent implements OnInit {
    private readonly raporService = inject(KdvHareketRaporuService);
    private readonly muhasebeRaporService = inject(MuhasebeRaporService);
    private readonly depolarService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);

    filter: KdvHareketRaporFilterModel = createDefaultKdvHareketRaporFilter();
    rapor: KdvHareketRaporModel | null = null;
    loading = false;
    loadingMessage = 'KDV hareket raporu yükleniyor...';

    tesisSecenekleri: TesisSecenek[] = [];
    tesisLoading = false;

    depoSecenekleri: Secenek<number>[] = [];
    tasinirKartSecenekleri: Secenek<number>[] = [];
    hareketTipiSecenekleri = STOK_HAREKET_TIPLERI;
    istisnaTanimSecenekleri: Secenek<number>[] = [];

    kdvUygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI;
    musFisDurumuSecenekleri = MUS_FIS_DURUMU_SECENEKLERI;

    // Toplu fiş oluşturma
    selectedRows: KdvHareketRaporSatirModel[] = [];

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
        this.filter = createDefaultKdvHareketRaporFilter();
        this.rapor = null;
        this.selectedRows = [];
    }

    loadRapor(): void {
        this.loading = true;
        this.rapor = null;
        this.selectedRows = [];
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

    navigateToFis(satir: KdvHareketRaporSatirModel): void {
        if (!satir.musFisId) return;
        this.router.navigate(['/muhasebe/fisler'], {
            queryParams: {
                id: satir.musFisId,
                fisNo: satir.musFisNo
            }
        });
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

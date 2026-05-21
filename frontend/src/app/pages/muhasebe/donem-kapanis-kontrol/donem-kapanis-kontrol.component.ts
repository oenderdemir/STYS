import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import {
    DonemKapanisKontrolFilterModel,
    DonemKapanisKontrolModel,
    DonemKapanisKontrolMaddeModel,
    createDefaultKapanisFilter
} from '../models/donem-kapanis-kontrol.model';
import { DonemKapanisKontrolService } from '../services/donem-kapanis-kontrol.service';
import { MuhasebeRaporService, MuhasebeTesisModel } from '../services/muhasebe-rapor.service';

interface TesisSecenek {
    label: string;
    value: number;
}

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const currentYear = new Date().getFullYear();
    const options: Array<{ label: string; value: number | null }> = [
        { label: 'Tümü', value: null }
    ];
    for (let y = currentYear - 3; y <= currentYear + 3; y++) {
        options.push({ label: String(y), value: y });
    }
    return options;
})();

const DONEM_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Seçiniz', value: null },
    ...Array.from({ length: 12 }, (_, i) => ({
        label: String(i + 1),
        value: i + 1
    }))
];

@Component({
    selector: 'app-donem-kapanis-kontrol',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        ButtonModule,
        CardModule,
        ProgressSpinnerModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule
    ],
    templateUrl: './donem-kapanis-kontrol.component.html',
    styleUrl: './donem-kapanis-kontrol.component.scss',
    providers: [MessageService]
})
export class DonemKapanisKontrolComponent implements OnInit {
    private readonly service = inject(DonemKapanisKontrolService);
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    loading = false;
    checking = false;

    filter: DonemKapanisKontrolFilterModel = createDefaultKapanisFilter();
    result: DonemKapanisKontrolModel | null = null;

    tesisSecenekleri: TesisSecenek[] = [];
    readonly maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    readonly donemSecenekleri = DONEM_SECENEKLERI;
    readonly filteredMaliYilSecenekleri = MALI_YIL_SECENEKLERI.filter(o => o.value !== null);
    readonly filteredDonemSecenekleri = DONEM_SECENEKLERI.filter(o => o.value !== null);

    ngOnInit(): void {
        this.loadTesisler();
    }

    private loadTesisler(): void {
        this.loading = true;
        this.raporService.getTesisler().pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler.map(t => ({
                    label: t.ad,
                    value: t.id
                }));

                // Query paramlardan filtreyi doldur
                const qp = this.route.snapshot.queryParamMap;
                const tesisIdParam = qp.get('tesisId');
                const maliYilParam = qp.get('maliYil');
                const donemNoParam = qp.get('donemNo');

                if (tesisIdParam) {
                    this.filter.tesisId = Number(tesisIdParam);
                } else if (this.tesisSecenekleri.length === 1) {
                    this.filter.tesisId = this.tesisSecenekleri[0].value;
                }

                if (maliYilParam) {
                    this.filter.maliYil = Number(maliYilParam);
                }

                if (donemNoParam) {
                    this.filter.donemNo = Number(donemNoParam);
                }

                // Eğer tüm query paramlar varsa otomatik kontrol çalıştır
                if (tesisIdParam && maliYilParam && donemNoParam) {
                    this.kontrolEt();
                }

                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    kontrolEt(): void {
        if (!this.filter.tesisId || this.filter.tesisId <= 0) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Lütfen tesis seçiniz.'
            });
            return;
        }

        if (!this.filter.maliYil || this.filter.maliYil < 2000) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Lütfen mali yıl seçiniz.'
            });
            return;
        }

        if (!this.filter.donemNo || this.filter.donemNo < 1) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Lütfen dönem seçiniz.'
            });
            return;
        }

        this.checking = true;
        this.result = null;

        this.service.kontrolEt(this.filter).pipe(finalize(() => {
            this.checking = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (response) => {
                this.result = response;
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    getSeverityTag(severity: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (severity) {
            case 'success': return 'success';
            case 'warn': return 'warn';
            case 'error':
            case 'danger': return 'danger';
            case 'info': return 'info';
            default: return 'secondary';
        }
    }

    getKapatilabilirSeverity(): 'success' | 'danger' {
        return this.result?.kapatilabilirMi ? 'success' : 'danger';
    }

    getKapatilabilirLabel(): string {
        return this.result?.kapatilabilirMi ? 'Kapatılabilir' : 'Kapatılamaz';
    }

    formatTarih(tarih: string): string {
        if (!tarih) return '-';
        const d = new Date(tarih);
        return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    formatPara(deger: number): string {
        return new Intl.NumberFormat('tr-TR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(deger);
    }

    getFarkSinifi(fark: number): string {
        if (Math.abs(fark) <= 0.009) return 'text-green-600';
        return 'text-red-600';
    }

    absFark(fark: number): number {
        return Math.abs(fark);
    }

    getFisTipiLabel(fisTipi: string): string {
        switch (fisTipi) {
            case 'Mahsup': return 'Mahsup';
            case 'Tahsil': return 'Tahsil';
            case 'Tediye': return 'Tediye';
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
            case 'Taslak': return 'warn';
            case 'Onayli': return 'success';
            case 'Iptal': return 'danger';
            case 'TersKayit': return 'info';
            default: return 'secondary';
        }
    }

    private showError(error: unknown): void {
        const msg = tryReadApiMessage(error);
        this.messageService.add({
            severity: UiSeverity.Error,
            summary: 'Hata',
            detail: msg ?? 'Bir hata oluştu.',
            life: 8000
        });
    }
}

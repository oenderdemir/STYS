import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    MuhasebeFisFilterModel,
    createDefaultFisFilter,
    normalizeFisFilter,
    MuhasebeFisDurumlari
} from '../models/muhasebe-fis.model';
import { YevmiyeDefteriModel, YevmiyeDefteriSatirModel } from '../models/yevmiye-defteri.model';
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';
import { Toolbar } from "primeng/toolbar";

interface DonemSecenek {
    label: string;
    value: number | null;
}

interface FisTipiSecenek {
    label: string;
    value: string | null;
}

interface DurumSecenek {
    label: string;
    value: string | null;
}

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const currentYear = new Date().getFullYear();
    const options: Array<{ label: string; value: number | null }> = [];
    for (let y = currentYear + 1; y >= currentYear - 5; y--) {
        options.push({ label: String(y), value: y });
    }
    return options;
})();

const DONEM_SECENEKLERI: DonemSecenek[] = [
    { label: 'Tümü', value: null },
    { label: 'Ocak', value: 1 },
    { label: 'Şubat', value: 2 },
    { label: 'Mart', value: 3 },
    { label: 'Nisan', value: 4 },
    { label: 'Mayıs', value: 5 },
    { label: 'Haziran', value: 6 },
    { label: 'Temmuz', value: 7 },
    { label: 'Ağustos', value: 8 },
    { label: 'Eylül', value: 9 },
    { label: 'Ekim', value: 10 },
    { label: 'Kasım', value: 11 },
    { label: 'Aralık', value: 12 }
];

const FIS_TIPI_SECENEKLERI: FisTipiSecenek[] = [
    { label: 'Tümü', value: null },
    { label: 'Mahsup', value: 'Mahsup' },
    { label: 'Tahsil', value: 'Tahsil' },
    { label: 'Tediye', value: 'Tediye' },
    { label: 'Devir', value: 'Devir' },
    { label: 'Açılış', value: 'Acilis' },
    { label: 'Stok', value: 'Stok' },
    { label: 'Taşınır', value: 'Tasinir' }
];

const DURUM_SECENEKLERI: DurumSecenek[] = [
    { label: 'Tümü', value: null },
    { label: 'Taslak', value: MuhasebeFisDurumlari.Taslak },
    { label: 'Onaylı', value: MuhasebeFisDurumlari.Onayli },
    { label: 'İptal', value: MuhasebeFisDurumlari.Iptal },
    { label: 'Ters Kayıt', value: MuhasebeFisDurumlari.TersKayit }
];

@Component({
    selector: 'app-yevmiye-defteri',
    standalone: true,
    imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
    MuhasebeTesisSecimDialogComponent,
    MuhasebeTesisContextBarComponent,
    Toolbar
],
    providers: [MessageService],
    templateUrl: './yevmiye-defteri.component.html',
    styleUrls: ['./yevmiye-defteri.component.scss']
})
export class YevmiyeDefteriComponent implements OnInit {
    private readonly raporService = inject(MuhasebeRaporService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: MuhasebeFisFilterModel = createDefaultFisFilter();
    result: YevmiyeDefteriModel | null = null;
    loading = false;
    exporting = false;

    maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    donemSecenekleri = DONEM_SECENEKLERI;
    fisTipiSecenekleri = FIS_TIPI_SECENEKLERI;
    durumSecenekleri = DURUM_SECENEKLERI;
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
    }

    ara(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        const normalized = normalizeFisFilter(this.filter);
        this.filter = normalized;
        this.result = null;
        this.loading = true;

        this.raporService.getYevmiyeDefteri(normalized).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (data) => {
                this.result = data;
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    temizle(): void {
        this.filter = createDefaultFisFilter();
        this.filter.tesisId = this.currentTesisId;
        this.clearResults();
    }

    getFark(): number {
        if (!this.result) return 0;
        return this.result.toplamBorc - this.result.toplamAlacak;
    }

    getDurumLabel(durum: string): string {
        switch (durum) {
            case MuhasebeFisDurumlari.Taslak: return 'Taslak';
            case MuhasebeFisDurumlari.Onayli: return 'Onaylı';
            case MuhasebeFisDurumlari.Iptal: return 'İptal';
            case MuhasebeFisDurumlari.TersKayit: return 'Ters Kayıt';
            default: return durum;
        }
    }

    getFisTipiLabel(fisTipi: string): string {
        switch (fisTipi) {
            case 'Mahsup': return 'Mahsup';
            case 'Tahsil': return 'Tahsil';
            case 'Tediye': return 'Tediye';
            case 'Devir': return 'Devir';
            case 'Acilis': return 'Açılış';
            case 'Stok': return 'Stok';
            case 'Tasinir': return 'Taşınır';
            default: return fisTipi;
        }
    }

    getDurumSeverity(durum: string): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (durum) {
            case MuhasebeFisDurumlari.Onayli: return 'success';
            case MuhasebeFisDurumlari.Taslak: return 'warn';
            case MuhasebeFisDurumlari.Iptal: return 'danger';
            case MuhasebeFisDurumlari.TersKayit: return 'info';
            default: return 'secondary';
        }
    }

    getFisTipiSeverity(fisTipi: string): 'info' | 'secondary' {
        switch (fisTipi) {
            case 'Mahsup': return 'info';
            default: return 'secondary';
        }
    }

    exportExcel(): void {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return;
        }

        this.filter.tesisId = tesisId;
        const normalized = normalizeFisFilter(this.filter);
        this.exporting = true;

        this.raporService.exportYevmiyeDefteriExcel(normalized).pipe(finalize(() => {
            this.exporting = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (blob) => {
                this.downloadBlob(blob, this.createExcelFileName());
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Excel dosyası indiriliyor.' });
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

    private createExcelFileName(): string {
        const now = new Date();
        const y = now.getFullYear();
        const mo = String(now.getMonth() + 1).padStart(2, '0');
        const d = String(now.getDate()).padStart(2, '0');
        const h = String(now.getHours()).padStart(2, '0');
        const mi = String(now.getMinutes()).padStart(2, '0');
        const s = String(now.getSeconds()).padStart(2, '0');
        return `yevmiye-defteri-${y}${mo}${d}-${h}${mi}${s}.xlsx`;
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
        if (error instanceof HttpErrorResponse) {
            this.messageService.add({ severity: 'error', summary: 'Hata', detail: error.message });
        } else if (error instanceof Error) {
            this.messageService.add({ severity: 'error', summary: 'Hata', detail: error.message });
        } else {
            this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Bir hata oluştu.' });
        }
    }
}

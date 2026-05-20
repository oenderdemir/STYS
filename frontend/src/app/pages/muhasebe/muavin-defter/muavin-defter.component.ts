import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import {
    MuavinDefterFilterModel,
    MuavinDefterModel,
    MuavinDefterSatirModel,
    createDefaultMuavinDefterFilter,
    normalizeMuavinDefterFilter
} from '../models/muavin-defter.model';
import { MuhasebeFisDurumlari } from '../models/muhasebe-fis.model';
import { MuhasebeRaporService, MuhasebeTesisModel } from '../services/muhasebe-rapor.service';
import { MuhasebeHesapPlaniService } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.service';
import { MuhasebeHesapPlaniModel } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.dto';

interface TesisSecenek {
    label: string;
    value: number;
}

interface DonemSecenek {
    label: string;
    value: number | null;
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

@Component({
    selector: 'app-muavin-defter',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule
    ],
    providers: [MessageService],
    templateUrl: './muavin-defter.component.html',
    styleUrls: ['./muavin-defter.component.scss']
})
export class MuavinDefterComponent implements OnInit {
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly hesapPlaniService = inject(MuhasebeHesapPlaniService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: MuavinDefterFilterModel = createDefaultMuavinDefterFilter();
    result: MuavinDefterModel | null = null;
    loading = false;
    exporting = false;

    hesapKoduInput: string | null = null;

    tesisSecenekleri: TesisSecenek[] = [];
    maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    donemSecenekleri = DONEM_SECENEKLERI;

    private hesapPlaniTree: MuhasebeHesapPlaniModel[] = [];
    private hesapPlaniLoaded = false;

    ngOnInit(): void {
        this.loadTesisler();
    }

    private loadTesisler(): void {
        this.raporService.getTesisler().pipe(finalize(() => {
            this.cdr.detectChanges();
        })).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler.map(t => ({
                    label: t.ad,
                    value: t.id
                }));
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private ensureHesapPlaniLoaded(): void {
        if (this.hesapPlaniLoaded) return;

        this.hesapPlaniService.getTree().subscribe({
            next: (tree) => {
                this.hesapPlaniTree = tree;
                this.hesapPlaniLoaded = true;
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private findHesapPlaniByKod(kod: string): MuhasebeHesapPlaniModel | undefined {
        const searchInTree = (nodes: MuhasebeHesapPlaniModel[]): MuhasebeHesapPlaniModel | undefined => {
            for (const node of nodes) {
                if (node.tamKod === kod) {
                    return node;
                }
            }
            return undefined;
        };
        return searchInTree(this.hesapPlaniTree);
    }

    private validateAndBuildFilter(): MuavinDefterFilterModel | null {
        if (!this.filter.tesisId) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Lütfen tesis seçiniz.' });
            return null;
        }

        const hesapKodu = (this.hesapKoduInput || '').trim();
        if (!hesapKodu) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Lütfen hesap kodu giriniz.' });
            return null;
        }

        if (!this.hesapPlaniLoaded) {
            this.ensureHesapPlaniLoaded();
            this.messageService.add({ severity: 'info', summary: 'Bilgi', detail: 'Hesap planı yükleniyor, lütfen tekrar "Ara" butonuna tıklayınız.' });
            return null;
        }

        const hesapPlani = this.findHesapPlaniByKod(hesapKodu);
        if (!hesapPlani || !hesapPlani.id) {
            this.messageService.add({ severity: 'error', summary: 'Hata', detail: `"${hesapKodu}" kodlu hesap bulunamadı.` });
            return null;
        }

        const normalized = normalizeMuavinDefterFilter(this.filter);
        normalized.muhasebeHesapPlaniId = hesapPlani.id;
        return normalized;
    }

    ara(): void {
        const normalized = this.validateAndBuildFilter();
        if (!normalized) return;

        this.filter = normalized;
        this.result = null;
        this.loading = true;

        this.raporService.getMuavinDefter(normalized).pipe(finalize(() => {
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

    exportExcel(): void {
        const normalized = this.validateAndBuildFilter();
        if (!normalized) return;

        this.exporting = true;

        this.raporService.exportMuavinDefterExcel(normalized).pipe(finalize(() => {
            this.exporting = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (blob) => {
                this.downloadBlob(blob, this.createExcelFileName());
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Muavin defter Excel dosyası indiriliyor.' });
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
        return `muavin-defter-${y}${mo}${d}-${h}${mi}${s}.xlsx`;
    }

    temizle(): void {
        this.filter = createDefaultMuavinDefterFilter();
        this.hesapKoduInput = null;
        this.result = null;
    }

    oncekiSayfa(): void {
        if (this.filter.page > 1) {
            this.filter.page--;
            this.ara();
        }
    }

    sonrakiSayfa(): void {
        this.filter.page++;
        this.ara();
    }

    onPageSizeChange(): void {
        this.filter.page = 1;
        this.ara();
    }

    getSayfaDurumu(): string {
        if (!this.result || !this.result.satirlar?.length) return '';
        const start = (this.filter.page - 1) * this.filter.pageSize + 1;
        const end = Math.min(this.filter.page * this.filter.pageSize, this.result.satirlar.length + (this.filter.page - 1) * this.filter.pageSize);
        return `${start}-${end}`;
    }

    getBakiyeTipiSeverity(bakiyeTipi: string): 'success' | 'danger' | 'secondary' {
        switch (bakiyeTipi) {
            case 'Borc': return 'danger';
            case 'Alacak': return 'success';
            default: return 'secondary';
        }
    }

    getBakiyeTipiLabel(bakiyeTipi: string): string {
        switch (bakiyeTipi) {
            case 'Borc': return 'Borç';
            case 'Alacak': return 'Alacak';
            case 'Sifir': return 'Sıfır';
            default: return bakiyeTipi;
        }
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

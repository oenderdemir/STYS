import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { ToolbarModule } from 'primeng/toolbar';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    MuavinDefterFilterModel,
    MuavinDefterModel,
    MuavinDefterSatirModel,
    createDefaultMuavinDefterFilter,
    normalizeMuavinDefterFilter
} from '../models/muavin-defter.model';
import { MuhasebeFisDurumlari } from '../models/muhasebe-fis.model';
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';
import { MuhasebeHesapPlaniService } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.service';
import { MuhasebeHesapPlaniModel } from '../muhasebe-hesap-plani/muhasebe-hesap-plani.dto';
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
        DatePickerModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    providers: [MessageService],
    templateUrl: './muavin-defter.component.html',
    styleUrls: ['./muavin-defter.component.scss']
})
export class MuavinDefterComponent implements OnInit {
    private readonly raporService = inject(MuhasebeRaporService);
    private readonly hesapPlaniService = inject(MuhasebeHesapPlaniService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    filter: MuavinDefterFilterModel = createDefaultMuavinDefterFilter();
    result: MuavinDefterModel | null = null;
    loading = false;
    exporting = false;

    hesapKoduInput: string | null = null;

    maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    donemSecenekleri = DONEM_SECENEKLERI;

    private hesapPlaniTree: MuhasebeHesapPlaniModel[] = [];
    private hesapPlaniLoaded = false;
    private hesapPlaniLoadPromise: Promise<void> | null = null;
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
                void this.showError(error);
            }
        });
    }

    private ensureHesapPlaniLoaded(): Promise<void> {
        if (this.hesapPlaniLoaded) {
            return Promise.resolve();
        }

        if (this.hesapPlaniLoadPromise) {
            return this.hesapPlaniLoadPromise;
        }

        this.hesapPlaniLoadPromise = new Promise<void>((resolve, reject) => {
            this.hesapPlaniService.getTree().pipe(
                finalize(() => {
                    this.hesapPlaniLoadPromise = null;
                    this.cdr.detectChanges();
                })
            ).subscribe({
                next: (tree) => {
                    this.hesapPlaniTree = tree;
                    this.hesapPlaniLoaded = true;
                    resolve();
                },
                error: (error: unknown) => {
                    void this.showError(error);
                    reject(error);
                }
            });
        });

        return this.hesapPlaniLoadPromise;
    }

    private findHesapPlaniByKod(kod: string): MuhasebeHesapPlaniModel | undefined {
        const normalizedKod = this.normalizeHesapKodu(kod);
        const searchInTree = (nodes: MuhasebeHesapPlaniModel[] | undefined): MuhasebeHesapPlaniModel | undefined => {
            if (!nodes?.length) {
                return undefined;
            }

            for (const node of nodes) {
                if (this.normalizeHesapKodu(node.tamKod) === normalizedKod) {
                    return node;
                }

                const childNodes = this.getHesapPlaniChildren(node);
                const found = searchInTree(childNodes);
                if (found) {
                    return found;
                }
            }
            return undefined;
        };

        return searchInTree(this.hesapPlaniTree);
    }

    private async validateAndBuildFilter(): Promise<MuavinDefterFilterModel | null> {
        const tesisId = this.tryGetSeciliTesisId();
        if (tesisId === null) {
            return null;
        }
        this.filter.tesisId = tesisId;

        const hesapKodu = (this.hesapKoduInput || '').trim();
        if (!hesapKodu) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Lütfen hesap kodu giriniz.' });
            return null;
        }

        if (!this.hesapPlaniLoaded) {
            try {
                await this.ensureHesapPlaniLoaded();
            } catch {
                return null;
            }
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

    async ara(): Promise<void> {
        const normalized = await this.validateAndBuildFilter();
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
                void this.showError(error);
            }
        });
    }

    async exportExcel(): Promise<void> {
        const normalized = await this.validateAndBuildFilter();
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
                void this.showError(error);
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
        this.filter.tesisId = this.currentTesisId;
        this.hesapKoduInput = null;
        this.clearResults();
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

    private normalizeHesapKodu(kod: string): string {
        return kod.trim().toLocaleLowerCase('tr-TR');
    }

    private getHesapPlaniChildren(node: MuhasebeHesapPlaniModel): MuhasebeHesapPlaniModel[] | undefined {
        const anyNode = node as MuhasebeHesapPlaniModel & {
            children?: MuhasebeHesapPlaniModel[];
            items?: MuhasebeHesapPlaniModel[];
            altHesaplar?: MuhasebeHesapPlaniModel[];
            childNodes?: MuhasebeHesapPlaniModel[];
        };

        return anyNode.children ?? anyNode.items ?? anyNode.altHesaplar ?? anyNode.childNodes ?? undefined;
    }

    private async showError(error: unknown): Promise<void> {
        const detail = await this.resolveErrorMessage(error);
        this.messageService.add({ severity: 'error', summary: 'Hata', detail });
    }

    private async resolveErrorMessage(error: unknown): Promise<string> {
        if (error instanceof HttpErrorResponse) {
            const parsed = await this.readHttpErrorMessage(error);
            if (parsed) {
                return parsed;
            }

            if (error.status === 0) {
                return 'Sunucuya ulaşılamadı.';
            }

            if (error.status) {
                return error.statusText?.trim().length
                    ? `HTTP ${error.status}: ${error.statusText}`
                    : `HTTP ${error.status}`;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message.trim();
        }

        if (typeof error === 'string' && error.trim().length > 0) {
            return error.trim();
        }

        return 'Bir hata oluştu.';
    }

    private async readHttpErrorMessage(error: HttpErrorResponse): Promise<string | null> {
        const payload = error.error;
        if (payload instanceof Blob) {
            try {
                const text = await payload.text();
                const parsed = this.tryReadStructuredMessage(text);
                if (parsed) {
                    return parsed;
                }
            } catch {
                return null;
            }
        }

        return this.tryReadStructuredMessage(payload) ?? (typeof error.message === 'string' ? error.message : null);
    }

    private tryReadStructuredMessage(payload: unknown): string | null {
        if (typeof payload === 'string') {
            const trimmed = payload.trim();
            if (!trimmed) {
                return null;
            }

            try {
                return this.tryReadStructuredMessage(JSON.parse(trimmed)) ?? trimmed;
            } catch {
                return trimmed;
            }
        }

        if (!this.isRecord(payload)) {
            return null;
        }

        const directKeys = ['message', 'detail', 'title', 'error'];
        for (const key of directKeys) {
            const value = payload[key];
            if (typeof value === 'string' && value.trim().length > 0) {
                return value.trim();
            }

            const nested = this.tryReadStructuredMessage(value);
            if (nested) {
                return nested;
            }
        }

        const errors = payload['errors'];
        if (Array.isArray(errors)) {
            for (const item of errors) {
                const nested = this.tryReadStructuredMessage(item);
                if (nested) {
                    return nested;
                }
            }
        }

        return null;
    }

    private isRecord(value: unknown): value is Record<string, unknown> {
        return typeof value === 'object' && value !== null;
    }
}

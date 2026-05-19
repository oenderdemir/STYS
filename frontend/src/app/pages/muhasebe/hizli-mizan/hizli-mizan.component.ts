import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import {
    MizanFilterModel,
    MizanKarsilastirmaModel,
    MizanKarsilastirmaSatirModel,
    MizanModel,
    MizanSatirModel,
    createDefaultMizanFilter,
    normalizeMizanFilter
} from '../models/mizan.model';
import { MuhasebeRaporService } from '../services/muhasebe-rapor.service';

interface TesisSecenek {
    label: string;
    value: number;
}

const DONEM_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Tum Donemler', value: null },
    { label: '1. Donem', value: 1 },
    { label: '2. Donem', value: 2 },
    { label: '3. Donem', value: 3 },
    { label: '4. Donem', value: 4 },
    { label: '5. Donem', value: 5 },
    { label: '6. Donem', value: 6 },
    { label: '7. Donem', value: 7 },
    { label: '8. Donem', value: 8 },
    { label: '9. Donem', value: 9 },
    { label: '10. Donem', value: 10 },
    { label: '11. Donem', value: 11 },
    { label: '12. Donem', value: 12 }
];

const PAGE_SIZE_SECENEKLERI: Array<{ label: string; value: number }> = [
    { label: '100', value: 100 },
    { label: '200', value: 200 },
    { label: '500', value: 500 },
    { label: '1000', value: 1000 }
];

@Component({
    selector: 'app-hizli-mizan',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DecimalPipe,
        ButtonModule,
        CheckboxModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule
    ],
    templateUrl: './hizli-mizan.component.html',
    styleUrls: ['./hizli-mizan.component.scss'],
    providers: [MessageService]
})
export class HizliMizanComponent implements OnInit {
    private readonly service = inject(MuhasebeRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    filter: MizanFilterModel = createDefaultMizanFilter();
    result: MizanModel | null = null;

    karsilastirmaLoading = false;
    karsilastirmaSonucu: MizanKarsilastirmaModel | null = null;

    tesisSecenekleri: TesisSecenek[] = [];
    readonly donemSecenekleri = DONEM_SECENEKLERI;
    readonly pageSizeSecenekleri = PAGE_SIZE_SECENEKLERI;

    ngOnInit(): void {
        this.loadTesisler();
    }

    private loadTesisler(): void {
        this.loading = true;
        this.service.getTesisler().pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler
                    .sort((a, b) => a.ad.localeCompare(b.ad))
                    .map((t) => ({ label: t.ad, value: t.id }));

                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    private validateFilter(): boolean {
        if (!this.filter.tesisId) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Tesis seçimi zorunludur.'
            });
            return false;
        }

        if (!this.filter.maliYil) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Mali yıl zorunludur.'
            });
            return false;
        }

        return true;
    }

    ara(): void {
        if (!this.validateFilter()) {
            return;
        }

        const normalizedFilter = normalizeMizanFilter({ ...this.filter });

        this.loading = true;
        this.result = null;
        this.karsilastirmaSonucu = null;
        this.service.getHizliMizan(normalizedFilter).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (mizan) => {
                this.result = mizan;
                this.filter.page = normalizedFilter.page;
                this.filter.pageSize = normalizedFilter.pageSize;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    karsilastir(): void {
        if (!this.validateFilter()) {
            return;
        }

        const normalizedFilter = normalizeMizanFilter({ ...this.filter });

        this.karsilastirmaLoading = true;
        this.karsilastirmaSonucu = null;

        this.service.karsilastirMizan(normalizedFilter)
            .pipe(finalize(() => {
                this.karsilastirmaLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    this.karsilastirmaSonucu = result;

                    if (result.eslesiyorMu) {
                        this.messageService.add({
                            severity: UiSeverity.Success,
                            summary: 'Karşılaştırma Başarılı',
                            detail: 'Eski mizan ve hızlı mizan sonuçları eşleşiyor.'
                        });
                    } else {
                        this.messageService.add({
                            severity: UiSeverity.Warn,
                            summary: 'Fark Bulundu',
                            detail: `${result.farkliSatirSayisi} satırda fark bulundu.`
                        });
                    }

                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
    }

    oncekiSayfa(): void {
        if (this.filter.page <= 1) {
            return;
        }
        this.filter.page--;
        this.ara();
    }

    onPageSizeChange(): void {
        this.filter.page = 1;
        this.ara();
    }

    sonrakiSayfa(): void {
        if (!this.result || this.result.satirlar.length < this.filter.pageSize) {
            return;
        }
        this.filter.page++;
        this.ara();
    }

    getBakiyeTipiSeverity(bakiyeTipi: string): 'success' | 'danger' | 'secondary' {
        switch (bakiyeTipi) {
            case 'Borc':
                return 'danger';
            case 'Alacak':
                return 'success';
            default:
                return 'secondary';
        }
    }

    getBakiyeTipiLabel(bakiyeTipi: string): string {
        switch (bakiyeTipi) {
            case 'Borc':
                return 'Borç';
            case 'Alacak':
                return 'Alacak';
            default:
                return 'Sıfır';
        }
    }

    getIndentStyle(seviye: number): Record<string, string> {
        const padding = (seviye - 1) * 1.5;
        return { 'padding-left': `${padding}rem` };
    }

    getKonsolideClass(satir: MizanSatirModel): string {
        return satir.konsolideSatirMi ? 'konsolide-satir' : '';
    }

    getKonsolideBadge(satir: MizanSatirModel): string {
        if (!satir.konsolideSatirMi) {
            return '';
        }
        return satir.hareketGorebilirMi ? 'Konsolide' : 'Sadece Konsolide';
    }

    getSayfaDurumu(): string {
        if (!this.result || this.result.satirlar.length === 0) {
            return '';
        }
        const start = (this.filter.page - 1) * this.filter.pageSize + 1;
        const end = start + this.result.satirlar.length - 1;
        return `${start}-${end}`;
    }

    sonrakiSayfaVarMi(): boolean {
        return !!(this.result && this.result.satirlar.length >= this.filter.pageSize);
    }

    getFarkTipiSeverity(farkTipi: string): 'warn' | 'info' | 'danger' | 'secondary' {
        switch (farkTipi) {
            case 'SadeceEskiMizandaVar':
                return 'warn';
            case 'SadeceHizliMizandaVar':
                return 'info';
            case 'TutarFarki':
                return 'danger';
            case 'HesapAdiFarki':
                return 'secondary';
            default:
                return 'secondary';
        }
    }

    getFarkTipiLabel(farkTipi: string): string {
        switch (farkTipi) {
            case 'SadeceEskiMizandaVar':
                return 'Sadece Eski Mizanda Var';
            case 'SadeceHizliMizandaVar':
                return 'Sadece Hızlı Mizanda Var';
            case 'TutarFarki':
                return 'Tutar Farkı';
            case 'HesapAdiFarki':
                return 'Hesap Adı Farkı';
            default:
                return farkTipi;
        }
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

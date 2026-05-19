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

    tesisSecenekleri: TesisSecenek[] = [];
    readonly donemSecenekleri = DONEM_SECENEKLERI;

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

                if (!this.filter.tesisId && this.tesisSecenekleri.length > 0) {
                    this.filter.tesisId = this.tesisSecenekleri[0].value;
                }
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    ara(): void {
        if (!this.filter.tesisId) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Tesis secimi zorunludur.'
            });
            return;
        }

        if (!this.filter.maliYil) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Mali yil zorunludur.'
            });
            return;
        }

        const normalizedFilter = normalizeMizanFilter({ ...this.filter });

        this.loading = true;
        this.result = null;
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

    oncekiSayfa(): void {
        if (this.filter.page <= 1) {
            return;
        }
        this.filter.page--;
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
                return 'Sifir';
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

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

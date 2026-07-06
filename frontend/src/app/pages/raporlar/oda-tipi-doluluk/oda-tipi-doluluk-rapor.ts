import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { OdaTipiDolulukRaporService } from './oda-tipi-doluluk-rapor.service';
import { OdaTipiDolulukRaporDto } from './oda-tipi-doluluk-rapor.dto';

@Component({
    selector: 'app-oda-tipi-doluluk-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, ChartModule, DatePickerModule, InputNumberModule, SelectModule, TableModule, ToastModule],
    providers: [MessageService],
    templateUrl: './oda-tipi-doluluk-rapor.html',
    styleUrl: './oda-tipi-doluluk-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OdaTipiDolulukRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(OdaTipiDolulukRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedBaslangic: Date = this.ayinIlkGunu();
    selectedBitis: Date = this.ayinSonGunu();
    selectedOdaTipiId: number | null = null;

    rapor: OdaTipiDolulukRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    expandedRowKeys: Record<number, boolean> = {};

    chartData: unknown = null;
    chartOptions: unknown = null;

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    private ayinIlkGunu(): Date {
        const simdi = new Date();
        return new Date(simdi.getFullYear(), simdi.getMonth(), 1);
    }

    private ayinSonGunu(): Date {
        const simdi = new Date();
        return new Date(simdi.getFullYear(), simdi.getMonth() + 1, 0);
    }

    private tesisleriYukle(): void {
        this.tesislerYukleniyor = true;
        this.rezervasyonService
            .getTesisler()
            .pipe(finalize(() => { this.tesislerYukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = tesisler;
                    if (tesisler.length > 0 && !this.selectedTesisId) {
                        this.selectedTesisId = tesisler[0].id;
                    }
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Tesis listesi alınamadı.'
                    });
                }
            });
    }

    raporGetir(): void {
        if (!this.selectedTesisId) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Lütfen bir tesis seçiniz.' });
            return;
        }

        if (this.selectedBaslangic > this.selectedBitis) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Başlangıç tarihi bitiş tarihinden büyük olamaz.' });
            return;
        }

        this.yukleniyor = true;
        this.rapor = null;
        this.expandedRowKeys = {};

        this.raporService
            .getRapor(
                this.selectedTesisId,
                this.tarihStringi(this.selectedBaslangic),
                this.tarihStringi(this.selectedBitis),
                this.selectedOdaTipiId
            )
            .pipe(finalize(() => { this.yukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (rapor) => {
                    this.rapor = rapor;
                    this.grafigiGuncelle(rapor);
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Rapor alınamadı.'
                    });
                }
            });
    }

    exportExcel(): void {
        if (!this.selectedTesisId) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Lütfen bir tesis seçiniz.' });
            return;
        }

        this.excelIndiriliyor = true;
        this.raporService
            .exportExcel(
                this.selectedTesisId,
                this.tarihStringi(this.selectedBaslangic),
                this.tarihStringi(this.selectedBitis),
                this.selectedOdaTipiId
            )
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `oda-tipi-doluluk-raporu-${this.tarihStringi(this.selectedBaslangic)}-${this.tarihStringi(this.selectedBitis)}.xlsx`);
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Excel dosyası indirilemedi.'
                    });
                }
            });
    }

    private grafigiGuncelle(rapor: OdaTipiDolulukRaporDto): void {
        const documentStyle = getComputedStyle(document.documentElement);
        const textMutedColor = documentStyle.getPropertyValue('--text-color-secondary');
        const borderColor = documentStyle.getPropertyValue('--surface-border');

        this.chartData = {
            labels: rapor.odaTipleri.map((x) => x.odaTipiAdi),
            datasets: [
                {
                    label: 'Doluluk Oranı (%)',
                    backgroundColor: documentStyle.getPropertyValue('--p-primary-400'),
                    data: rapor.odaTipleri.map((x) => x.dolulukOrani)
                }
            ]
        };

        this.chartOptions = {
            maintainAspectRatio: false,
            aspectRatio: 0.6,
            plugins: {
                legend: {
                    labels: { color: textMutedColor }
                }
            },
            scales: {
                x: {
                    ticks: { color: textMutedColor },
                    grid: { color: 'transparent', borderColor: 'transparent' }
                },
                y: {
                    beginAtZero: true,
                    max: 100,
                    ticks: { color: textMutedColor },
                    grid: { color: borderColor, borderColor: 'transparent' }
                }
            }
        };
    }

    private tarihStringi(tarih: Date): string {
        const yil = tarih.getFullYear();
        const ay = String(tarih.getMonth() + 1).padStart(2, '0');
        const gun = String(tarih.getDate()).padStart(2, '0');
        return `${yil}-${ay}-${gun}`;
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
}

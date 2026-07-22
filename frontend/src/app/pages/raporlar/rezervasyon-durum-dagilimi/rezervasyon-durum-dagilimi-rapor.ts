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
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonOdaTipiDto, RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { RezervasyonDurumDagilimiRaporService } from './rezervasyon-durum-dagilimi-rapor.service';
import { RezervasyonDurumDagilimiRaporDto } from './rezervasyon-durum-dagilimi-rapor.dto';

interface DurumSecenegi {
    label: string;
    value: string;
}

@Component({
    selector: 'app-rezervasyon-durum-dagilimi-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, ChartModule, DatePickerModule, SelectModule, TableModule, ToastModule, TagModule],
    providers: [MessageService],
    templateUrl: './rezervasyon-durum-dagilimi-rapor.html',
    styleUrl: './rezervasyon-durum-dagilimi-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class RezervasyonDurumDagilimiRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(RezervasyonDurumDagilimiRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    odaTipleri: RezervasyonOdaTipiDto[] = [];
    odaTipleriYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedBaslangic: Date = this.ayinIlkGunu();
    selectedBitis: Date = this.ayinSonGunu();
    selectedOdaTipiId: number | null = null;
    selectedDurum: string | null = null;

    rapor: RezervasyonDurumDagilimiRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    durumDagilimiChartData: unknown = null;
    durumDagilimiChartOptions: unknown = null;
    odaTipiChartData: unknown = null;
    odaTipiChartOptions: unknown = null;

    readonly durumSecenekleri: DurumSecenegi[] = [
        { label: 'Taslak', value: 'taslak' },
        { label: 'Onaylı', value: 'onayli' },
        { label: 'Check-in Tamamlandı', value: 'check-in-tamamlandi' },
        { label: 'Check-out Tamamlandı', value: 'check-out-tamamlandi' },
        { label: 'İptal', value: 'iptal' }
    ];

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    get odaTipiSecenekleri(): { label: string; value: number | null }[] {
        return [{ label: 'Tümü', value: null }, ...this.odaTipleri.map((x) => ({ label: x.ad, value: x.id }))];
    }

    onTesisChange(tesisId: number | null): void {
        this.selectedTesisId = tesisId;
        this.selectedOdaTipiId = null;
        this.odaTipleri = [];
        if (tesisId) {
            this.odaTipleriniYukle(tesisId);
        }
    }

    private odaTipleriniYukle(tesisId: number): void {
        this.odaTipleriYukleniyor = true;
        this.rezervasyonService
            .getOdaTipleriByTesis(tesisId)
            .pipe(finalize(() => { this.odaTipleriYukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (odaTipleri) => {
                    this.odaTipleri = odaTipleri;
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Oda tipi listesi alınamadı.'
                    });
                }
            });
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
                        this.odaTipleriniYukle(this.selectedTesisId);
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

        this.raporService
            .getRapor(
                this.selectedTesisId,
                this.tarihStringi(this.selectedBaslangic),
                this.tarihStringi(this.selectedBitis),
                this.selectedOdaTipiId,
                this.selectedDurum
            )
            .pipe(finalize(() => { this.yukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (rapor) => {
                    this.rapor = rapor;
                    this.grafikleriGuncelle(rapor);
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
                this.selectedOdaTipiId,
                this.selectedDurum
            )
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `rezervasyon-durum-dagilimi-raporu-${this.tarihStringi(this.selectedBaslangic)}-${this.tarihStringi(this.selectedBitis)}.xlsx`);
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

    durumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
        switch (durum) {
            case 'CheckOutTamamlandi':
                return 'success';
            case 'CheckInTamamlandi':
                return 'info';
            case 'Onayli':
                return 'warn';
            case 'Iptal':
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private grafikleriGuncelle(rapor: RezervasyonDurumDagilimiRaporDto): void {
        const documentStyle = getComputedStyle(document.documentElement);
        const textMutedColor = documentStyle.getPropertyValue('--text-color-secondary');
        const borderColor = documentStyle.getPropertyValue('--surface-border');

        this.durumDagilimiChartData = {
            labels: rapor.durumlar.map((x) => x.durumLabel),
            datasets: [
                {
                    data: rapor.durumlar.map((x) => x.rezervasyonSayisi),
                    backgroundColor: [
                        documentStyle.getPropertyValue('--p-gray-400'),
                        documentStyle.getPropertyValue('--p-yellow-400'),
                        documentStyle.getPropertyValue('--p-blue-400'),
                        documentStyle.getPropertyValue('--p-green-400'),
                        documentStyle.getPropertyValue('--p-red-400')
                    ]
                }
            ]
        };

        this.durumDagilimiChartOptions = {
            plugins: {
                legend: {
                    labels: { color: textMutedColor }
                }
            }
        };

        this.odaTipiChartData = {
            labels: rapor.odaTipleri.map((x) => x.odaTipiAdi),
            datasets: [
                {
                    label: 'Gerçekleşen',
                    backgroundColor: documentStyle.getPropertyValue('--p-green-400'),
                    data: rapor.odaTipleri.map((x) => x.gerceklesenSayisi)
                },
                {
                    label: 'İptal',
                    backgroundColor: documentStyle.getPropertyValue('--p-red-400'),
                    data: rapor.odaTipleri.map((x) => x.iptalSayisi)
                }
            ]
        };

        this.odaTipiChartOptions = {
            maintainAspectRatio: false,
            aspectRatio: 0.8,
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

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
import { OrtalamaKonaklamaSuresiRaporService } from './ortalama-konaklama-suresi-rapor.service';
import { OrtalamaKonaklamaSuresiRaporDto } from './ortalama-konaklama-suresi-rapor.dto';

@Component({
    selector: 'app-ortalama-konaklama-suresi-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, ChartModule, DatePickerModule, SelectModule, TableModule, ToastModule, TagModule],
    providers: [MessageService],
    templateUrl: './ortalama-konaklama-suresi-rapor.html',
    styleUrl: './ortalama-konaklama-suresi-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrtalamaKonaklamaSuresiRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(OrtalamaKonaklamaSuresiRaporService);
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

    rapor: OrtalamaKonaklamaSuresiRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    chartData: unknown = null;
    chartOptions: unknown = null;

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
                    this.downloadBlob(blob, `ortalama-konaklama-suresi-raporu-${this.tarihStringi(this.selectedBaslangic)}-${this.tarihStringi(this.selectedBitis)}.xlsx`);
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

    konaklamaGrubuSeverity(konaklamaGrubu: string): 'success' | 'info' | 'warn' {
        switch (konaklamaGrubu) {
            case 'kisa':
                return 'success';
            case 'orta':
                return 'info';
            default:
                return 'warn';
        }
    }

    private grafigiGuncelle(rapor: OrtalamaKonaklamaSuresiRaporDto): void {
        const documentStyle = getComputedStyle(document.documentElement);
        const textMutedColor = documentStyle.getPropertyValue('--text-color-secondary');

        this.chartData = {
            labels: ['Kısa Konaklama', 'Orta Konaklama', 'Uzun Konaklama'],
            datasets: [
                {
                    data: [rapor.ozet.kisaKonaklamaSayisi, rapor.ozet.ortaKonaklamaSayisi, rapor.ozet.uzunKonaklamaSayisi],
                    backgroundColor: [
                        documentStyle.getPropertyValue('--p-green-400'),
                        documentStyle.getPropertyValue('--p-blue-400'),
                        documentStyle.getPropertyValue('--p-orange-400')
                    ]
                }
            ]
        };

        this.chartOptions = {
            plugins: {
                legend: {
                    labels: { color: textMutedColor }
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

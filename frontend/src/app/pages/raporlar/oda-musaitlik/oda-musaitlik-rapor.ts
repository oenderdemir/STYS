import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { OdaMusaitlikRaporService } from './oda-musaitlik-rapor.service';
import { OdaMusaitlikRaporDto } from './oda-musaitlik-rapor.dto';

interface DurumSecenegi {
    label: string;
    value: string;
}

@Component({
    selector: 'app-oda-musaitlik-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, DatePickerModule, InputNumberModule, SelectModule, TableModule, ToastModule, TagModule],
    providers: [MessageService],
    templateUrl: './oda-musaitlik-rapor.html',
    styleUrl: './oda-musaitlik-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OdaMusaitlikRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(OdaMusaitlikRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedBaslangic: Date = new Date();
    selectedBitis: Date = this.gunEkle(new Date(), 7);
    selectedDurum = 'tumu';
    selectedOdaTipiId: number | null = null;
    selectedKapasite: number | null = null;

    rapor: OdaMusaitlikRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    readonly durumSecenekleri: DurumSecenegi[] = [
        { label: 'Tümü', value: 'tumu' },
        { label: 'Tamamen Boş', value: 'tamamen-bos' },
        { label: 'Tamamen Dolu', value: 'tamamen-dolu' },
        { label: 'Kısmen Müsait', value: 'kismen-musait' }
    ];

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    private gunEkle(tarih: Date, gunSayisi: number): Date {
        const yeniTarih = new Date(tarih);
        yeniTarih.setDate(yeniTarih.getDate() + gunSayisi);
        return yeniTarih;
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

        this.raporService
            .getRapor(
                this.selectedTesisId,
                this.tarihStringi(this.selectedBaslangic),
                this.tarihStringi(this.selectedBitis),
                this.selectedDurum,
                this.selectedOdaTipiId,
                this.selectedKapasite
            )
            .pipe(finalize(() => { this.yukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (rapor) => {
                    this.rapor = rapor;
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
                this.selectedDurum,
                this.selectedOdaTipiId,
                this.selectedKapasite
            )
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `oda-musaitlik-raporu-${this.tarihStringi(this.selectedBaslangic)}-${this.tarihStringi(this.selectedBitis)}.xlsx`);
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

    musaitlikDurumuSeverity(musaitlikDurumu: string): 'success' | 'warn' | 'danger' | 'info' {
        switch (musaitlikDurumu) {
            case 'tamamen-bos':
                return 'success';
            case 'tamamen-dolu':
                return 'danger';
            default:
                return 'warn';
        }
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

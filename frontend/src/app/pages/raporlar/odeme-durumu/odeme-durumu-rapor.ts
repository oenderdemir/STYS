import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { OdemeDurumuRaporService } from './odeme-durumu-rapor.service';
import { OdemeDurumuRaporDto } from './odeme-durumu-rapor.dto';

interface OdemeDurumuFiltreSecenegi {
    label: string;
    value: string;
}

@Component({
    selector: 'app-odeme-durumu-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, DatePickerModule, SelectModule, TableModule, ToastModule, TagModule],
    providers: [MessageService],
    templateUrl: './odeme-durumu-rapor.html',
    styleUrl: './odeme-durumu-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OdemeDurumuRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(OdemeDurumuRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedBaslangic: Date = this.ayinIlkGunu();
    selectedBitis: Date = new Date();
    selectedOdemeDurumu = 'borclu';

    rapor: OdemeDurumuRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    readonly odemeDurumuSecenekleri: OdemeDurumuFiltreSecenegi[] = [
        { label: 'Borçlu', value: 'borclu' },
        { label: 'Tümü', value: 'tumu' },
        { label: 'Ödemesi Yok', value: 'odemesi-yok' },
        { label: 'Kısmi Ödendi', value: 'kismi-odendi' },
        { label: 'Tamamen Ödendi', value: 'tamamen-odendi' },
        { label: 'Çıkış Yapmış Borçlu', value: 'cikis-yapmis-borclu' }
    ];

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    private ayinIlkGunu(): Date {
        const simdi = new Date();
        return new Date(simdi.getFullYear(), simdi.getMonth(), 1);
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
            .getRapor(this.selectedTesisId, this.tarihStringi(this.selectedBaslangic), this.tarihStringi(this.selectedBitis), this.selectedOdemeDurumu)
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
            .exportExcel(this.selectedTesisId, this.tarihStringi(this.selectedBaslangic), this.tarihStringi(this.selectedBitis), this.selectedOdemeDurumu)
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `odeme-durumu-raporu-${this.tarihStringi(this.selectedBaslangic)}-${this.tarihStringi(this.selectedBitis)}.xlsx`);
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

    odemeDurumuSeverity(odemeDurumu: string): 'success' | 'warn' | 'danger' | 'info' {
        switch (odemeDurumu) {
            case 'tamamen-odendi':
                return 'success';
            case 'kismi-odendi':
                return 'warn';
            case 'odemesi-yok':
                return 'danger';
            default:
                return 'info';
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

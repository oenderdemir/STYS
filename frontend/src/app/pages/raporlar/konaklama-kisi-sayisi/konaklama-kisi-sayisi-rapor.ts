import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { KonaklamaKisiSayisiRaporService } from './konaklama-kisi-sayisi-rapor.service';
import { KonaklamaKisiSayisiRaporDto } from './konaklama-kisi-sayisi-rapor.dto';

interface SecenekOgesi {
    label: string;
    value: number;
}

@Component({
    selector: 'app-konaklama-kisi-sayisi-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, SelectModule, ToastModule],
    providers: [MessageService],
    templateUrl: './konaklama-kisi-sayisi-rapor.html',
    styleUrl: './konaklama-kisi-sayisi-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class KonaklamaKisiSayisiRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(KonaklamaKisiSayisiRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedAy: number = new Date().getMonth() + 1;
    selectedBaslangicYil: number = new Date().getFullYear() - 1;
    selectedBitisYil: number = new Date().getFullYear();

    rapor: KonaklamaKisiSayisiRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    readonly aySecenekleri: SecenekOgesi[] = [
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

    readonly yilSecenekleri: SecenekOgesi[] = this.buildYilSecenekleri();

    ngOnInit(): void {
        this.tesisleriYukle();
    }

    private buildYilSecenekleri(): SecenekOgesi[] {
        const suankiYil = new Date().getFullYear();
        const secenekler: SecenekOgesi[] = [];
        for (let yil = suankiYil - 5; yil <= suankiYil + 1; yil++) {
            secenekler.push({ label: String(yil), value: yil });
        }
        return secenekler;
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

        if (this.selectedBaslangicYil > this.selectedBitisYil) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Başlangıç yılı bitiş yılından büyük olamaz.' });
            return;
        }

        this.yukleniyor = true;
        this.rapor = null;

        this.raporService
            .getRapor(this.selectedTesisId, this.selectedAy, this.selectedBaslangicYil, this.selectedBitisYil)
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
            .exportExcel(this.selectedTesisId, this.selectedAy, this.selectedBaslangicYil, this.selectedBitisYil)
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `konaklama-kisi-sayisi-${this.selectedBaslangicYil}-${this.selectedBitisYil}-${this.selectedAy}.xlsx`);
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

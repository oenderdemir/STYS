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
import { RezervasyonOdaTipiDto, RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { GecikenCheckInRaporService } from './geciken-check-in-rapor.service';
import { GecikenCheckInRaporDto } from './geciken-check-in-rapor.dto';

interface GecikmeDurumuSecenegi {
    label: string;
    value: string;
}

@Component({
    selector: 'app-geciken-check-in-rapor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CardModule, DatePickerModule, SelectModule, TableModule, ToastModule, TagModule],
    providers: [MessageService],
    templateUrl: './geciken-check-in-rapor.html',
    styleUrl: './geciken-check-in-rapor.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class GecikenCheckInRaporComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(GecikenCheckInRaporService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    odaTipleri: RezervasyonOdaTipiDto[] = [];
    odaTipleriYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedReferansTarihi: Date = new Date();
    selectedOdaTipiId: number | null = null;
    selectedGecikmeDurumu = 'tumu';

    rapor: GecikenCheckInRaporDto | null = null;
    yukleniyor = false;
    excelIndiriliyor = false;

    readonly gecikmeDurumuSecenekleri: GecikmeDurumuSecenegi[] = [
        { label: 'Tümü', value: 'tumu' },
        { label: 'Bugün Giriş', value: 'bugun-giris' },
        { label: 'Geciken', value: 'geciken' },
        { label: 'Kritik Geciken', value: 'kritik-geciken' }
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

        this.yukleniyor = true;
        this.rapor = null;

        this.raporService
            .getRapor(
                this.selectedTesisId,
                this.tarihStringi(this.selectedReferansTarihi),
                this.selectedOdaTipiId,
                this.selectedGecikmeDurumu
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
                this.tarihStringi(this.selectedReferansTarihi),
                this.selectedOdaTipiId,
                this.selectedGecikmeDurumu
            )
            .pipe(finalize(() => { this.excelIndiriliyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (blob) => {
                    this.downloadBlob(blob, `geciken-check-in-raporu-${this.tarihStringi(this.selectedReferansTarihi)}.xlsx`);
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

    gecikmeDurumuSeverity(gecikmeDurumu: string): 'info' | 'warn' | 'danger' {
        switch (gecikmeDurumu) {
            case 'bugun-giris':
                return 'info';
            case 'geciken':
                return 'warn';
            default:
                return 'danger';
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

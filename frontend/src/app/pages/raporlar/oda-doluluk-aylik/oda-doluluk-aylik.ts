import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../core/api';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.service';
import { RezervasyonTesisDto } from '../../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { OdaDolulukAylikRaporService } from './oda-doluluk-aylik.service';
import { AylikOdaDolulukRaporDto, OdaDolulukHucreDto } from './oda-doluluk-aylik.dto';

interface SecenekOgesi {
    label: string;
    value: number;
}

@Component({
    selector: 'app-oda-doluluk-aylik',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CardModule,
        CheckboxModule,
        DialogModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        TooltipModule
    ],
    providers: [MessageService],
    templateUrl: './oda-doluluk-aylik.html',
    styleUrl: './oda-doluluk-aylik.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class OdaDolulukAylikComponent implements OnInit {
    private readonly rezervasyonService = inject(RezervasyonYonetimiService);
    private readonly raporService = inject(OdaDolulukAylikRaporService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    tesislerYukleniyor = false;

    selectedTesisId: number | null = null;
    selectedYil: number = new Date().getFullYear();
    selectedAy: number = new Date().getMonth() + 1;
    maskele = false;

    rapor: AylikOdaDolulukRaporDto | null = null;
    yukleniyor = false;

    cakismaDialogVisible = false;
    cakismaDialogOdaNo = '';
    cakismaDialogTarih = '';
    cakismaDialogListesi: OdaDolulukHucreDto['cakismalar'] = [];

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
        for (let yil = suankiYil - 2; yil <= suankiYil + 2; yil++) {
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

        this.yukleniyor = true;
        this.rapor = null;

        this.raporService
            .getAylikRapor(this.selectedTesisId, this.selectedYil, this.selectedAy, this.maskele)
            .pipe(finalize(() => { this.yukleniyor = false; this.cdr.markForCheck(); }))
            .subscribe({
                next: (rapor) => {
                    this.rapor = rapor;
                },
                error: (err: HttpErrorResponse) => {
                    this.messageService.add({
                        severity: 'error',
                        summary: 'Hata',
                        detail: tryReadApiMessage(err) ?? 'Aylık oda doluluk raporu alınamadı.'
                    });
                }
            });
    }

    hucreTikla(hucre: OdaDolulukHucreDto, gunTarihi: string): void {
        if (!hucre.doluMu) {
            return;
        }

        if (hucre.cakismaVarMi) {
            this.cakismaDialogAc(hucre, gunTarihi);
            return;
        }

        if (!hucre.rezervasyonId) {
            return;
        }
        this.router.navigate(['/rezervasyon-yonetimi'], { queryParams: { rezervasyonId: hucre.rezervasyonId } });
    }

    private cakismaDialogAc(hucre: OdaDolulukHucreDto, gunTarihi: string): void {
        this.cakismaDialogOdaNo = hucre.odaNo;
        this.cakismaDialogTarih = this.formatTarihKisa(gunTarihi);
        this.cakismaDialogListesi = hucre.cakismalar;
        this.cakismaDialogVisible = true;
    }

    cakismaDialogKapat(): void {
        this.cakismaDialogVisible = false;
        this.cakismaDialogListesi = [];
    }

    cakismaRezervasyonuAc(rezervasyonId: number): void {
        this.router.navigate(['/rezervasyon-yonetimi'], { queryParams: { rezervasyonId } });
        this.cakismaDialogKapat();
    }

    hucreSeverity(hucre: OdaDolulukHucreDto): 'success' | 'warn' | 'danger' | 'info' | 'secondary' {
        switch (hucre.hucreRenkKodu) {
            case 'payment-missing':
                return 'danger';
            case 'conflict':
                return 'danger';
            case 'checked-out':
                return 'secondary';
            case 'occupied':
                return 'success';
            case 'reserved':
                return 'info';
            default:
                return 'secondary';
        }
    }

    formatGunBasligi(tarihIso: string, gunAdi: string): string {
        return `${this.formatTarihKisa(tarihIso)} ${gunAdi}`;
    }

    private formatTarihKisa(tarihIso: string): string {
        const tarih = new Date(tarihIso);
        return `${tarih.getDate()} ${this.aySecenekleri[tarih.getMonth()].label} ${tarih.getFullYear()}`;
    }

    formatPara(tutar: number, paraBirimi: string | null | undefined): string {
        return new Intl.NumberFormat('tr-TR', {
            style: 'currency',
            currency: paraBirimi ?? 'TRY',
            minimumFractionDigits: 2
        }).format(tutar);
    }

    durumLabel(durum: string): string {
        const map: Record<string, string> = {
            Taslak: 'Taslak',
            Onayli: 'Onaylı',
            CheckInTamamlandi: 'Check-in Yapıldı',
            CheckOutTamamlandi: 'Check-out Yapıldı',
            Iptal: 'İptal'
        };
        return map[durum] ?? durum;
    }

    exportExcel(): void {
        this.messageService.add({ severity: 'info', summary: 'Yakında', detail: 'Excel export özelliği yakında eklenecek.' });
    }

    exportPdf(): void {
        this.messageService.add({ severity: 'info', summary: 'Yakında', detail: 'PDF export özelliği yakında eklenecek.' });
    }
}

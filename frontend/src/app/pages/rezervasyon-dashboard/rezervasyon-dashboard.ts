import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import {
    RezervasyonDashboardDto,
    RezervasyonGelirKirilimDto,
    RezervasyonKpiTrendGunDto,
    RezervasyonKpiOzetDto,
    RezervasyonTesisDto
} from '../rezervasyon-yonetimi/rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../rezervasyon-yonetimi/rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './rezervasyon-dashboard.html',
    providers: [MessageService]
})
export class RezervasyonDashboard implements OnInit {
    private readonly service = inject(RezervasyonYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: RezervasyonTesisDto[] = [];
    selectedTesisId: number | null = null;
    selectedTarih = this.todayInput();
    kpiBaslangicTarihi = this.firstDayOfMonthInput();
    kpiBitisTarihi = this.todayInput();
    reportSelectedTesisIds: number[] = [];
    reportBaslangicTarihi = this.firstDayOfMonthInput();
    reportBitisTarihi = this.todayInput();
    dashboard: RezervasyonDashboardDto | null = null;

    loadingReferences = false;
    loadingDashboard = false;
    exportingExcel = false;
    exportingPdf = false;

    private readonly durumTaslak = 'Taslak';
    private readonly durumOnayli = 'Onayli';
    private readonly durumCheckInTamamlandi = 'CheckInTamamlandi';
    private readonly durumCheckOutTamamlandi = 'CheckOutTamamlandi';
    private readonly durumIptal = 'Iptal';

    get canView(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.View');
    }

    get canManage(): boolean {
        return this.authService.hasPermission('RezervasyonYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        if (this.selectedTesisId && this.selectedTesisId > 0) {
            this.loadDashboard();
            return;
        }

        this.loadReferences();
    }

    onTesisChange(): void {
        this.loadDashboard();
    }

    onTarihChange(): void {
        if (this.selectedTarih && this.selectedTarih.trim().length > 0) {
            this.kpiBitisTarihi = this.selectedTarih;
        }
        this.loadDashboard();
    }

    onKpiRangeChange(): void {
        this.loadDashboard();
    }

    exportOdemeRaporuExcel(): void {
        this.exportOdemeRapor('excel');
    }

    exportOdemeRaporuPdf(): void {
        this.exportOdemeRapor('pdf');
    }

    formatDateTime(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return date.toLocaleString('tr-TR');
    }

    formatNumber(value: number): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        return safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
    }

    formatPercent(value: number): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        return `${safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
    }

    formatMoney(value: number): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        return safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    getKpiOzet(): RezervasyonKpiOzetDto {
        return this.dashboard?.kpiOzet ?? {
            tarihAraligiGunSayisi: 0,
            toplamRezervasyonSayisi: 0,
            iptalRezervasyonSayisi: 0,
            iptalOraniYuzde: 0,
            toplamGeceSayisi: 0,
            satilanGeceSayisi: 0,
            dolulukOraniYuzde: 0,
            toplamGelir: 0,
            adr: 0,
            revPar: 0
        };
    }

    getDurumKirilimiToplam(): number {
        return (this.dashboard?.durumaGoreRezervasyonKirilimi ?? []).reduce((toplam, item) => toplam + (Number.isFinite(item.tutar) ? item.tutar : 0), 0);
    }

    getDurumKirilimiOran(item: RezervasyonGelirKirilimDto): string {
        const toplam = this.getDurumKirilimiToplam();
        if (toplam <= 0) {
            return this.formatPercent(0);
        }

        return this.formatPercent((item.tutar * 100) / toplam);
    }

    getKpiTrend(): RezervasyonKpiTrendGunDto[] {
        return this.dashboard?.kpiTrendGunluk ?? [];
    }

    getTrendPoints(type: 'gelir' | 'doluluk'): string {
        const values = this.getKpiTrendValues(type);
        if (values.length === 0) {
            return '';
        }

        if (values.length === 1) {
            const y = this.clampSparklineY(values[0], values[0], values[0]);
            return `2,${y} 98,${y}`;
        }

        const min = Math.min(...values);
        const max = Math.max(...values);
        const width = 96;
        const height = 24;
        const step = width / (values.length - 1);

        return values
            .map((value, index) => {
                const x = 2 + (step * index);
                const y = this.clampSparklineY(value, min, max, height);
                return `${x.toFixed(2)},${y.toFixed(2)}`;
            })
            .join(' ');
    }

    getTrendMarkerItems(type: 'gelir' | 'doluluk'): Array<{ x: number; y: number; tooltip: string }> {
        const trend = this.getKpiTrend();
        if (trend.length === 0) {
            return [];
        }

        const values = this.getKpiTrendValues(type);
        const min = Math.min(...values);
        const max = Math.max(...values);
        const width = 96;
        const height = 24;
        const step = trend.length > 1 ? width / (trend.length - 1) : 0;

        return trend.map((item, index) => {
            const value = type === 'gelir' ? item.gelir : item.dolulukOraniYuzde;
            const safeValue = Number.isFinite(value) ? value : 0;
            const x = trend.length > 1 ? 2 + (step * index) : 50;
            const y = this.clampSparklineY(safeValue, min, max, height);
            const valueText = type === 'gelir'
                ? `${this.formatMoney(safeValue)} TRY`
                : this.formatPercent(safeValue);

            return {
                x,
                y,
                tooltip: `${this.formatShortDate(item.tarih)} - ${valueText}`
            };
        });
    }

    getTrendStartLabel(): string {
        const trend = this.getKpiTrend();
        if (trend.length === 0) {
            return '-';
        }

        return this.formatShortDate(trend[0].tarih);
    }

    getTrendEndLabel(): string {
        const trend = this.getKpiTrend();
        if (trend.length === 0) {
            return '-';
        }

        return this.formatShortDate(trend[trend.length - 1].tarih);
    }

    getTrendValuesPreview(type: 'gelir' | 'doluluk'): string {
        const values = this.getKpiTrendValues(type);
        if (values.length === 0) {
            return '-';
        }

        const first = values[0];
        const last = values[values.length - 1];
        const diff = last - first;
        const sign = diff >= 0 ? '+' : '';
        if (type === 'gelir') {
            return `${sign}${this.formatMoney(diff)} TRY`;
        }

        return `${sign}${this.formatPercent(diff)}`;
    }

    getRezervasyonDurumLabel(durum: string): string {
        switch (durum) {
            case this.durumTaslak:
                return 'Taslak';
            case this.durumOnayli:
                return 'Onaylı';
            case this.durumCheckInTamamlandi:
                return 'Giriş Yapıldı';
            case this.durumCheckOutTamamlandi:
                return 'Çıkış Yapıldı';
            case this.durumIptal:
                return 'İptal';
            default:
                return durum;
        }
    }

    getRezervasyonDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        switch (durum) {
            case this.durumTaslak:
                return 'secondary';
            case this.durumOnayli:
                return 'info';
            case this.durumCheckInTamamlandi:
                return 'warn';
            case this.durumCheckOutTamamlandi:
                return 'success';
            case this.durumIptal:
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private loadReferences(): void {
        this.loadingReferences = true;
        this.service
            .getTesisler()
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = [...tesisler].sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
                    if (this.selectedTesisId && !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId && this.tesisler.length > 0) {
                        this.selectedTesisId = this.tesisler[0].id;
                    }

                    if (this.reportSelectedTesisIds.length === 0 && this.selectedTesisId) {
                        this.reportSelectedTesisIds = [this.selectedTesisId];
                    }

                    if (this.selectedTarih && this.selectedTarih.trim().length > 0) {
                        this.kpiBitisTarihi = this.selectedTarih;
                    }

                    this.loadDashboard();
                },
                error: (error: unknown) => {
                    this.tesisler = [];
                    this.selectedTesisId = null;
                    this.reportSelectedTesisIds = [];
                    this.dashboard = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private getKpiTrendValues(type: 'gelir' | 'doluluk'): number[] {
        const trend = this.getKpiTrend();
        if (trend.length === 0) {
            return [];
        }

        return trend.map((item) => {
            const value = type === 'gelir' ? item.gelir : item.dolulukOraniYuzde;
            return Number.isFinite(value) ? value : 0;
        });
    }

    private clampSparklineY(value: number, min: number, max: number, height = 24): number {
        if (max <= min) {
            return height / 2;
        }

        const normalized = (value - min) / (max - min);
        return 2 + ((1 - normalized) * (height - 4));
    }

    private formatShortDate(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return value;
        }

        return date.toLocaleDateString('tr-TR');
    }

    private loadDashboard(): void {
        if (!this.canView) {
            this.dashboard = null;
            return;
        }

        if (!this.selectedTesisId || this.selectedTesisId <= 0) {
            this.dashboard = null;
            return;
        }

        this.loadingDashboard = true;
        this.service
            .getGunlukDashboard(
                this.selectedTesisId,
                this.selectedTarih,
                this.kpiBaslangicTarihi,
                this.kpiBitisTarihi)
            .pipe(
                finalize(() => {
                    this.loadingDashboard = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (dashboard) => {
                    this.dashboard = dashboard;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.dashboard = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private todayInput(): string {
        const date = new Date();
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    private firstDayOfMonthInput(): string {
        const date = new Date();
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        return `${year}-${month}-01`;
    }

    private exportOdemeRapor(format: 'excel' | 'pdf'): void {
        if (!this.canManage) {
            return;
        }

        if (this.reportSelectedTesisIds.length === 0) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Rapor icin en az bir tesis seciniz.' });
            return;
        }

        if (!this.reportBaslangicTarihi || !this.reportBitisTarihi || this.reportBaslangicTarihi > this.reportBitisTarihi) {
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: 'Gecerli bir tarih araligi seciniz.' });
            return;
        }

        if (format === 'excel') {
            this.exportingExcel = true;
        } else {
            this.exportingPdf = true;
        }

        const export$ = format === 'excel'
            ? this.service.exportOdemeRaporuExcel(this.reportSelectedTesisIds, this.reportBaslangicTarihi, this.reportBitisTarihi)
            : this.service.exportOdemeRaporuPdf(this.reportSelectedTesisIds, this.reportBaslangicTarihi, this.reportBitisTarihi);

        export$
            .pipe(
                finalize(() => {
                    this.exportingExcel = false;
                    this.exportingPdf = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (blob) => {
                    const extension = format === 'excel' ? 'xls' : 'pdf';
                    const fileName = `odeme-raporu-${this.reportBaslangicTarihi}-${this.reportBitisTarihi}.${extension}`;
                    this.downloadBlob(blob, fileName);
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Rapor indirildi.' });
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                }
            });
    }

    private downloadBlob(blob: Blob, fileName: string): void {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}

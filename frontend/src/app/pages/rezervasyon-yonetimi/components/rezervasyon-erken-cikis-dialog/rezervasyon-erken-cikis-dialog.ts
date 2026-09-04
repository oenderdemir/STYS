import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
    ChangeDetectionStrategy,
    ChangeDetectorRef,
    Component,
    EventEmitter,
    Input,
    OnChanges,
    Output,
    SimpleChanges,
    inject
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../../core/api';
import { UiSeverity } from '../../../../core/ui/ui-severity.constants';
import { RezervasyonErkenCikisOzetDto } from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-erken-cikis-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DatePickerModule, DialogModule, TagModule],
    providers: [ConfirmationService],
    templateUrl: './rezervasyon-erken-cikis-dialog.html',
    styleUrl: './rezervasyon-erken-cikis-dialog.scss'
})
export class RezervasyonErkenCikisDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Input() rezervasyonDurumu: string | null = null;
    @Input() mevcutCikisTarihi: string | null = null;
    @Input() tesisCikisSaati: string | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() saved = new EventEmitter<void>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    yeniCikisTarihi: Date | null = null;
    loading = false;
    saving = false;
    ozet: RezervasyonErkenCikisOzetDto | null = null;

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible']) {
            this.reset();
        }
    }

    kapat(): void {
        this.visibleChange.emit(false);
        this.reset();
    }

    onYeniCikisTarihiChange(value: Date | null): void {
        this.yeniCikisTarihi = this.applyTesisCikisSaati(value);
    }

    canGetirOnizleme(): boolean {
        return !this.loading && !this.saving && !!this.rezervasyonId && this.isYeniCikisTarihiGecerli();
    }

    getirOnizleme(): void {
        if (!this.rezervasyonId || !this.canGetirOnizleme()) {
            return;
        }

        this.loading = true;
        this.ozet = null;
        this.service
            .getErkenCikisOnizleme(this.rezervasyonId, { yeniCikisTarihi: this.toLocalDateTimeString(this.yeniCikisTarihi) })
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (result) => {
                    this.ozet = result;
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    this.ozet = null;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    canKaydet(): boolean {
        return !this.loading && !this.saving && !!this.ozet && !!this.rezervasyonId;
    }

    onayla(): void {
        if (!this.canKaydet() || !this.ozet || !this.rezervasyonId || !this.yeniCikisTarihi) {
            return;
        }

        const mesaj = [
            `Mevcut cikis: ${this.formatDateTime(this.ozet.eskiCikisTarihi)}`,
            `Yeni cikis: ${this.formatDateTime(this.ozet.yeniCikisTarihi)}`,
            `Konaklama farki: ${this.formatCurrency(this.ozet.fiyatFarki, this.ozet.paraBirimi)}`,
            this.ozet.fazlaTahsilat > 0
                ? `Fazla tahsilat: ${this.formatCurrency(this.ozet.fazlaTahsilat, this.ozet.paraBirimi)}`
                : `Kalan bakiye: ${this.formatCurrency(this.ozet.kalanBakiye, this.ozet.paraBirimi)}`
        ].join('. ');

        this.confirmationService.confirm({
            header: 'Erken Cikisi Onayla',
            message: mesaj,
            icon: 'pi pi-calendar-minus',
            acceptLabel: 'Onayla',
            rejectLabel: 'Vazgec',
            accept: () => this.executeKaydet()
        });
    }

    formatCurrency(value: number, currency: string): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        const safeCurrency = (currency || 'TRY').toUpperCase();
        return `${safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${safeCurrency}`;
    }

    formatDateTime(value: string | Date | null | undefined): string {
        const date = this.parseApiDateTime(value);
        if (!date) {
            return '-';
        }

        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).format(date);
    }

    get bakiyeDurumEtiketi(): string {
        if (!this.ozet) {
            return '-';
        }

        if (this.ozet.fazlaTahsilat > 0) {
            return 'Fazla tahsilat var';
        }

        if (this.ozet.kalanBakiye > 0) {
            return 'Kalan bakiye var';
        }

        return 'Bakiye sifir';
    }

    get bakiyeSeverity(): 'danger' | 'warn' | 'success' {
        if (!this.ozet) {
            return 'warn';
        }

        if (this.ozet.fazlaTahsilat > 0) {
            return 'danger';
        }

        if (this.ozet.kalanBakiye > 0) {
            return 'warn';
        }

        return 'success';
    }

    private executeKaydet(): void {
        if (!this.rezervasyonId || !this.yeniCikisTarihi) {
            return;
        }

        this.saving = true;
        this.service
            .kaydetErkenCikis(this.rezervasyonId, { yeniCikisTarihi: this.toLocalDateTimeString(this.yeniCikisTarihi) })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (result) => {
                    this.messageService.add({
                        severity: UiSeverity.Success,
                        summary: 'Basarili',
                        detail: `Rezervasyon yeni cikis tarihi ${this.formatDateTime(result.yeniCikisTarihi)} olacak sekilde guncellendi.`
                    });
                    this.saved.emit();
                    this.kapat();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    private isYeniCikisTarihiGecerli(): boolean {
        if (!this.yeniCikisTarihi) {
            return false;
        }

        const mevcut = this.parseApiDateTime(this.mevcutCikisTarihi);
        if (!mevcut) {
            return true;
        }

        return this.yeniCikisTarihi.getTime() < mevcut.getTime();
    }

    private applyTesisCikisSaati(value: Date | null): Date | null {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
            return null;
        }

        const parts = (this.tesisCikisSaati ?? '').split(':').map((x) => Number.parseInt(x, 10));
        const hour = Number.isFinite(parts[0]) ? parts[0] : 0;
        const minute = Number.isFinite(parts[1]) ? parts[1] : 0;
        const second = Number.isFinite(parts[2]) ? parts[2] : 0;
        return new Date(value.getFullYear(), value.getMonth(), value.getDate(), hour, minute, second);
    }

    private toLocalDateTimeString(value: Date | null | undefined): string {
        if (!(value instanceof Date) || Number.isNaN(value.getTime())) {
            return '';
        }

        const year = String(value.getFullYear()).padStart(4, '0');
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        const hour = String(value.getHours()).padStart(2, '0');
        const minute = String(value.getMinutes()).padStart(2, '0');
        const second = String(value.getSeconds()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}:${second}`;
    }

    private parseApiDateTime(value: string | Date | null | undefined): Date | null {
        if (!value) {
            return null;
        }

        if (value instanceof Date) {
            return Number.isNaN(value.getTime()) ? null : new Date(value.getTime());
        }

        const normalized = value.trim();
        if (normalized.length === 0) {
            return null;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
            const [yearText, monthText, dayText] = normalized.split('-');
            const year = Number.parseInt(yearText, 10);
            const month = Number.parseInt(monthText, 10);
            const day = Number.parseInt(dayText, 10);
            const localDate = new Date(year, month - 1, day);
            return Number.isNaN(localDate.getTime()) ? null : localDate;
        }

        const parsed = new Date(normalized);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    private reset(): void {
        this.yeniCikisTarihi = null;
        this.loading = false;
        this.saving = false;
        this.ozet = null;
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

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
import { RezervasyonUzatmaSecenekleriDto, RezervasyonUzatmaSecenegiDto } from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';

const SENARYO_TIPI_ETIKETLERI: Record<string, string> = {
    AyniOdadaDevam: 'Aynı odada devam',
    CheckoutGunundeOdaDegisimi: 'Çıkış gününde oda değişimi',
    UzatmaSirasindaOdaDegisimi: 'Uzatma sırasında oda değişimi'
};

@Component({
    selector: 'app-rezervasyon-uzatma-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DatePickerModule, DialogModule, TagModule],
    providers: [ConfirmationService],
    templateUrl: './rezervasyon-uzatma-dialog.html',
    styleUrl: './rezervasyon-uzatma-dialog.scss'
})
export class RezervasyonUzatmaDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Input() rezervasyonDurumu: string | null = null;
    @Input() mevcutCikisTarihi: string | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() saved = new EventEmitter<void>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    yeniCikisTarihi: Date | null = null;
    loading = false;
    saving = false;
    secenekler: RezervasyonUzatmaSecenekleriDto | null = null;
    seciliSecenek: RezervasyonUzatmaSecenegiDto | null = null;

    private loadSeq = 0;

    private readonly sonucKoduMusaitlikYok = 'MusaitlikYok';

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible'] && this.visible) {
            this.reset();
            return;
        }

        if (changes['visible'] && !this.visible) {
            this.reset();
        }
    }

    kapat(): void {
        this.visibleChange.emit(false);
        this.reset();
    }

    isYeniCikisTarihiGecerli(): boolean {
        if (!this.yeniCikisTarihi) {
            return false;
        }

        const mevcut = this.parseApiDateTime(this.mevcutCikisTarihi);
        if (!mevcut) {
            return true;
        }

        return this.yeniCikisTarihi.getTime() > mevcut.getTime();
    }

    canGetirSecenekleri(): boolean {
        if (this.loading || this.saving || !this.rezervasyonId) {
            return false;
        }

        return this.isYeniCikisTarihiGecerli();
    }

    getirSecenekleri(): void {
        if (!this.rezervasyonId || !this.canGetirSecenekleri()) {
            return;
        }

        const seq = ++this.loadSeq;
        this.loading = true;
        this.secenekler = null;
        this.seciliSecenek = null;
        this.service
            .getUzatmaSecenekleri(this.rezervasyonId, { yeniCikisTarihi: this.toLocalDateTimeString(this.yeniCikisTarihi) })
            .pipe(
                finalize(() => {
                    if (seq === this.loadSeq) {
                        this.loading = false;
                        this.cdr.markForCheck();
                    }
                })
            )
            .subscribe({
                next: (result) => {
                    if (seq !== this.loadSeq) return;
                    this.secenekler = result;
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    if (seq !== this.loadSeq) return;
                    this.secenekler = null;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    seciliSecenegiSec(secenek: RezervasyonUzatmaSecenegiDto): void {
        if (this.saving) return;
        this.seciliSecenek = secenek;
    }

    canUzat(): boolean {
        return !this.loading && !this.saving && !!this.seciliSecenek;
    }

    rezervasyonuUzatOnayla(): void {
        if (this.saving || !this.seciliSecenek || !this.rezervasyonId || !this.yeniCikisTarihi) {
            return;
        }

        const secenek = this.seciliSecenek;
        const eskiTarihMetni = this.formatDateTime(this.mevcutCikisTarihi);
        const yeniTarihMetni = this.formatDateTime(this.yeniCikisTarihi);
        const senaryoEtiketi = this.getSenaryoTipiEtiketi(secenek.senaryoTipi);
        const ekUcretMetni = this.formatCurrency(secenek.ekNihaiUcret, secenek.paraBirimi);

        this.confirmationService.confirm({
            header: 'Rezervasyonu Uzat',
            message: `${eskiTarihMetni} tarihinden ${yeniTarihMetni} tarihine uzatilacak. Senaryo: ${senaryoEtiketi}. Oda degisikligi sayisi: ${secenek.odaDegisimSayisi}. Eklenecek ucret: ${ekUcretMetni}. Onayliyor musunuz?`,
            icon: 'pi pi-calendar-plus',
            acceptLabel: 'Uzat',
            rejectLabel: 'Vazgec',
            accept: () => this.executeUzat(secenek)
        });
    }

    getSenaryoTipiEtiketi(senaryoTipi: string): string {
        return SENARYO_TIPI_ETIKETLERI[senaryoTipi] ?? senaryoTipi;
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

    private executeUzat(secenek: RezervasyonUzatmaSecenegiDto): void {
        if (this.saving || !this.rezervasyonId || !this.yeniCikisTarihi) {
            return;
        }

        this.saving = true;
        this.service
            .uzatRezervasyon(this.rezervasyonId, {
                yeniCikisTarihi: this.toLocalDateTimeString(this.yeniCikisTarihi),
                senaryoKodu: secenek.senaryoKodu
            })
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
                        detail: `Rezervasyon uzatildi. Yeni cikis tarihi: ${this.formatDateTime(result.yeniCikisTarihi)}.`
                    });
                    this.saved.emit();
                    this.kapat();
                },
                error: (error: unknown) => {
                    if (error instanceof HttpErrorResponse && error.status === 409) {
                        // Plan artik gecerli degil: eski secimi temizle, secenekleri AYNI yeni cikis
                        // tarihiyle yeniden getir - baska bir secenegi OTOMATIK olarak kaydetme.
                        // NOT: finalize(...) geri cagrisi (saving=false) bu error callback'inden
                        // SONRA calisir (RxJS teardown sirasi) - bu yuzden getirSecenekleri()'nin
                        // canGetirSecenekleri() kontrolunun eskimis (hala true olan) saving bayragina
                        // takilip SESSIZCE hicbir sey yapmamasini onlemek icin burada ACIKCA sifirlanir.
                        this.saving = false;
                        this.messageService.add({
                            severity: UiSeverity.Warn,
                            summary: 'Plan artik gecerli degil',
                            detail: 'Secilen uzatma planinin musaitligi degisti. Secenekler yeniden getiriliyor.'
                        });
                        this.seciliSecenek = null;
                        this.secenekler = null;
                        this.getirSecenekleri();
                        return;
                    }

                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    get sonucMusaitlikYokMu(): boolean {
        return this.secenekler?.sonucKodu === this.sonucKoduMusaitlikYok;
    }

    private reset(): void {
        ++this.loadSeq;
        this.yeniCikisTarihi = null;
        this.loading = false;
        this.saving = false;
        this.secenekler = null;
        this.seciliSecenek = null;
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

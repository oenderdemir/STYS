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
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { tryReadApiMessage } from '../../../../core/api';
import { UiSeverity } from '../../../../core/ui/ui-severity.constants';
import {
    RezervasyonOdaDegisimAdayOdaDto,
    RezervasyonOdaDegisimKayitDto,
    RezervasyonOdaDegisimKonaklayanDto,
    RezervasyonOdaDegisimSecenekDto
} from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-oda-degisimi-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, SelectModule, TableModule],
    templateUrl: './rezervasyon-oda-degisimi-dialog.html',
    styleUrl: './rezervasyon-oda-degisimi-dialog.scss'
})
export class RezervasyonOdaDegisimiDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Input() rezervasyonDurumu: string | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() saved = new EventEmitter<void>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    odaDegisimSecenekleri: RezervasyonOdaDegisimSecenekDto | null = null;
    odaDegisimSecimleri: Record<number, number> = {};

    private loadSeq = 0;

    private readonly durumTaslak = 'Taslak';
    private readonly durumOnayli = 'Onayli';
    private readonly durumCheckInTamamlandi = 'CheckInTamamlandi';

    get bilgiMesaji(): string | null {
        if (this.rezervasyonDurumu === this.durumCheckInTamamlandi) {
            return 'Bu rezervasyon check-in yapmis durumda. Oda degisimi kaydedildiginde ilgili konaklayan atamalari yeni odaya otomatik tasinacaktir.';
        }

        if (this.rezervasyonDurumu === this.durumTaslak || this.rezervasyonDurumu === this.durumOnayli) {
            return 'Bu ekran check-in oncesi planlanan oda atamasini gucellemek icin kullanilir.';
        }

        return null;
    }

    ngOnChanges(changes: SimpleChanges): void {
        const shouldLoad =
            this.visible &&
            !!this.rezervasyonId &&
            (changes['visible'] || changes['rezervasyonId']);

        if (shouldLoad) {
            this.resetSelectionState();
            this.load(this.rezervasyonId!);
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

    kaydet(): void {
        if (this.saving) return;
        if (!this.rezervasyonId || !this.odaDegisimSecenekleri || !this.canKaydet()) {
            return;
        }

        this.saving = true;
        this.service
            .saveOdaDegisimi(this.rezervasyonId, {
                atamalar: this.odaDegisimSecenekleri.kayitlar.map((item) => ({
                    rezervasyonSegmentOdaAtamaId: item.rezervasyonSegmentOdaAtamaId,
                    yeniOdaId: this.getSeciliOdaId(item)
                }))
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
                        detail: `Oda degisimi kaydedildi. Referans: ${result.referansNo}`
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

    canKaydet(): boolean {
        if (this.loading || this.saving || !this.odaDegisimSecenekleri) {
            return false;
        }

        if (this.odaDegisimSecenekleri.kayitlar.length === 0) {
            return false;
        }

        return this.odaDegisimSecenekleri.kayitlar.every((kayit) =>
            kayit.adayOdalar.length > 0 && this.getSeciliOdaId(kayit) > 0);
    }

    getAdayOptions(kayit: RezervasyonOdaDegisimKayitDto): { label: string; value: number }[] {
        return kayit.adayOdalar.map((aday) => ({
            value: aday.odaId,
            label: `${aday.odaNo} - ${aday.binaAdi} (${aday.odaTipiAdi}, kalan ${aday.kalanKapasite}${aday.paylasimliMi && aday.onerilenYatakNolari.length > 0 ? `, yatak ${aday.onerilenYatakNolari.join(', ')}` : ''})`
        }));
    }

    getSelectedAday(kayit: RezervasyonOdaDegisimKayitDto): RezervasyonOdaDegisimAdayOdaDto | null {
        const seciliOdaId = this.getSeciliOdaId(kayit);
        if (seciliOdaId <= 0) {
            return null;
        }

        return kayit.adayOdalar.find((x) => x.odaId === seciliOdaId) ?? null;
    }

    getKonaklayanOnerileri(
        kayit: RezervasyonOdaDegisimKayitDto,
        aday: RezervasyonOdaDegisimAdayOdaDto | null
    ): Array<{ konaklayan: RezervasyonOdaDegisimKonaklayanDto; onerilenYatakNo: number | null }> {
        if (!aday) {
            return [];
        }

        return kayit.tasinacakKonaklayanlar.map((konaklayan, index) => ({
            konaklayan,
            onerilenYatakNo: aday.paylasimliMi ? (aday.onerilenYatakNolari[index] ?? null) : null
        }));
    }

    getTasinacakKonaklayanAdlari(kayit: RezervasyonOdaDegisimKayitDto): string {
        return kayit.tasinacakKonaklayanlar.map((x) => x.adSoyad).join(', ');
    }

    getSeciliOdaId(kayit: RezervasyonOdaDegisimKayitDto): number {
        return this.odaDegisimSecimleri[kayit.rezervasyonSegmentOdaAtamaId] ?? 0;
    }

    setSeciliOdaId(kayit: RezervasyonOdaDegisimKayitDto, odaId: number): void {
        this.odaDegisimSecimleri[kayit.rezervasyonSegmentOdaAtamaId] = odaId;
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

    private load(rezervasyonId: number): void {
        const seq = ++this.loadSeq;
        this.loading = true;
        this.service
            .getOdaDegisimSecenekleri(rezervasyonId)
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
                    if (seq === this.loadSeq) {
                        this.odaDegisimSecenekleri = result;
                        this.odaDegisimSecimleri = {};
                        for (const kayit of result.kayitlar) {
                            const firstCandidate = kayit.adayOdalar[0];
                            if (firstCandidate && firstCandidate.odaId > 0) {
                                this.odaDegisimSecimleri[kayit.rezervasyonSegmentOdaAtamaId] = firstCandidate.odaId;
                            }
                        }
                        this.cdr.markForCheck();
                    }
                },
                error: (error: unknown) => {
                    if (seq === this.loadSeq) {
                        this.odaDegisimSecenekleri = null;
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                        this.cdr.markForCheck();
                    }
                }
            });
    }

    private reset(): void {
        ++this.loadSeq;
        this.loading = false;
        this.saving = false;
        this.odaDegisimSecenekleri = null;
        this.odaDegisimSecimleri = {};
    }

    private resetSelectionState(): void {
        this.odaDegisimSecenekleri = null;
        this.odaDegisimSecimleri = {};
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

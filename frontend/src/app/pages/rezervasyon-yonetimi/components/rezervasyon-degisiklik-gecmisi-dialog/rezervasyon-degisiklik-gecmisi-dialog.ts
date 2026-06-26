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
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../../core/api';
import { UiSeverity } from '../../../../core/ui/ui-severity.constants';
import { RezervasyonDegisiklikGecmisiDto } from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';

interface DegisiklikPayloadTableColumn {
    field: string;
    header: string;
}

interface DegisiklikPayloadTableData {
    columns: DegisiklikPayloadTableColumn[];
    rows: Record<string, string>[];
}

@Component({
    selector: 'app-rezervasyon-degisiklik-gecmisi-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [CommonModule, ButtonModule, DialogModule, TableModule, TagModule],
    templateUrl: './rezervasyon-degisiklik-gecmisi-dialog.html',
    styleUrl: './rezervasyon-degisiklik-gecmisi-dialog.scss'
})
export class RezervasyonDegisiklikGecmisiDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Output() visibleChange = new EventEmitter<boolean>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    kayitlar: RezervasyonDegisiklikGecmisiDto[] = [];
    payloadDialogVisible = false;
    payloadDialogTitle = '';
    payloadDialogContent = '';
    payloadDialogMode: 'table' | 'json' = 'table';
    payloadTableColumns: DegisiklikPayloadTableColumn[] = [];
    payloadTableRows: Record<string, string>[] = [];

    private loadSeq = 0;

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible']) {
            if (this.visible && this.rezervasyonId) {
                this.kayitlar = [];
                this.closePayloadDialog();
                this.load(this.rezervasyonId);
            } else if (!this.visible) {
                this.reset();
            }
        }
    }

    kapat(): void {
        this.visibleChange.emit(false);
        this.reset();
    }

    getIslemLabel(islemTipi: string): string {
        switch (islemTipi) {
            case 'RezervasyonOlusturuldu':
                return 'Rezervasyon Oluşturuldu';
            case 'KonaklayanPlaniKaydedildi':
                return 'Konaklayan Planı Kaydedildi';
            case 'OdaDegisimiYapildi':
                return 'Oda Değişimi Yapıldı';
            case 'CheckInTamamlandi':
                return 'Check-in Tamamlandı';
            case 'CheckOutTamamlandi':
                return 'Check-out Tamamlandı';
            case 'IptalEdildi':
                return 'Rezervasyon İptal Edildi';
            case 'IptalGeriAlindi':
                return 'İptal Geri Alındı';
            case 'OdemeKaydedildi':
                return 'Ödeme Kaydedildi';
            case 'KonaklamaHaklariUretildi':
                return 'Konaklama Hakları Üretildi';
            case 'KonaklamaHakkiDurumuGuncellendi':
                return 'Konaklama Hakkı Durumu Güncellendi';
            case 'KonaklamaHakkiTuketimiKaydedildi':
                return 'Konaklama Hakkı Tüketimi Kaydedildi';
            case 'KonaklamaHakkiTuketimiSilindi':
                return 'Konaklama Hakkı Tüketimi Silindi';
            default:
                return islemTipi;
        }
    }

    getDegisiklikOzet(kayit: RezervasyonDegisiklikGecmisiDto): string {
        const oncekiVar = this.hasJsonPayload(kayit.oncekiDegerJson);
        const yeniVar = this.hasJsonPayload(kayit.yeniDegerJson);

        if (!oncekiVar && !yeniVar) {
            return 'Detay yok';
        }

        if (oncekiVar && yeniVar) {
            return 'Onceki + Yeni';
        }

        return oncekiVar ? 'Sadece Onceki' : 'Sadece Yeni';
    }

    hasJsonPayload(value: string | null | undefined): boolean {
        return !!value && value.trim().length > 0 && value.trim() !== '[]' && value.trim() !== '{}';
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

    openPayloadDialog(kayit: RezervasyonDegisiklikGecmisiDto, payloadType: 'onceki' | 'yeni'): void {
        const payload = payloadType === 'onceki' ? kayit.oncekiDegerJson : kayit.yeniDegerJson;
        if (!this.hasJsonPayload(payload)) {
            return;
        }
        const payloadValue = payload ?? '';

        this.payloadDialogTitle = `${this.getIslemLabel(kayit.islemTipi)} - ${payloadType === 'onceki' ? 'Onceki Deger' : 'Yeni Deger'}`;
        const parsedPayload = this.tryParseJson(payloadValue);
        const tableData = this.tryBuildKonaklayanPlaniTableData(parsedPayload) ?? this.tryBuildGenericTableData(parsedPayload);

        if (tableData) {
            this.payloadDialogMode = 'table';
            this.payloadTableColumns = tableData.columns;
            this.payloadTableRows = tableData.rows;
            this.payloadDialogContent = '';
        } else {
            this.payloadDialogMode = 'json';
            this.payloadTableColumns = [];
            this.payloadTableRows = [];
            this.payloadDialogContent = this.formatJsonForDisplay(payloadValue);
        }
        this.payloadDialogVisible = true;
    }

    closePayloadDialog(): void {
        this.payloadDialogVisible = false;
        this.payloadDialogTitle = '';
        this.payloadDialogContent = '';
        this.payloadDialogMode = 'table';
        this.payloadTableColumns = [];
        this.payloadTableRows = [];
    }

    private load(rezervasyonId: number): void {
        const seq = ++this.loadSeq;
        this.loading = true;
        this.service
            .getDegisiklikGecmisi(rezervasyonId)
            .pipe(
                finalize(() => {
                    if (seq === this.loadSeq) {
                        this.loading = false;
                        this.cdr.markForCheck();
                    }
                })
            )
            .subscribe({
                next: (items) => {
                    if (seq === this.loadSeq) {
                        this.kayitlar = items;
                        this.cdr.markForCheck();
                    }
                },
                error: (error: unknown) => {
                    if (seq === this.loadSeq) {
                        this.kayitlar = [];
                        this.messageService.add({
                            severity: UiSeverity.Error,
                            summary: 'Hata',
                            detail: this.resolveErrorMessage(error)
                        });
                        this.cdr.markForCheck();
                    }
                }
            });
    }

    private reset(): void {
        this.loading = false;
        this.kayitlar = [];
        this.closePayloadDialog();
    }

    private formatJsonForDisplay(value: string | null | undefined): string {
        if (!value || value.trim().length === 0) {
            return '-';
        }

        try {
            return JSON.stringify(JSON.parse(value), null, 2);
        } catch {
            return value;
        }
    }

    private tryParseJson(value: string): unknown {
        try {
            return JSON.parse(value);
        } catch {
            return null;
        }
    }

    private tryBuildKonaklayanPlaniTableData(payload: unknown): DegisiklikPayloadTableData | null {
        if (!Array.isArray(payload) || payload.length === 0) {
            return null;
        }

        const items = payload.filter((x): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x));
        if (items.length !== payload.length) {
            return null;
        }

        const rows: Record<string, string>[] = [];
        for (const item of items) {
            const siraNo = this.readUnknown(item, ['SiraNo', 'siraNo']);
            const adSoyad = this.readUnknown(item, ['AdSoyad', 'adSoyad']);
            const cinsiyet = this.readUnknown(item, ['Cinsiyet', 'cinsiyet']);
            const katilimDurumu = this.readUnknown(item, ['KatilimDurumu', 'katilimDurumu']);
            const tcKimlikNo = this.readUnknown(item, ['TcKimlikNo', 'tcKimlikNo']);
            const pasaportNo = this.readUnknown(item, ['PasaportNo', 'pasaportNo']);
            const atamalarUnknown = this.readUnknown(item, ['Atamalar', 'atamalar']);

            if (typeof siraNo === 'undefined' || typeof adSoyad === 'undefined' || !Array.isArray(atamalarUnknown)) {
                return null;
            }

            const atamaOzetleri = atamalarUnknown
                .filter((a): a is Record<string, unknown> => !!a && typeof a === 'object' && !Array.isArray(a))
                .map((atama) => {
                    const segmentId = this.readUnknown(atama, ['RezervasyonSegmentId', 'rezervasyonSegmentId', 'SegmentId', 'segmentId']);
                    const odaId = this.readUnknown(atama, ['OdaId', 'odaId']);
                    const yatakNo = this.readUnknown(atama, ['YatakNo', 'yatakNo']);
                    const yatakText = typeof yatakNo === 'undefined' || yatakNo === null
                        ? ''
                        : ` (Yatak ${this.toDisplayString(yatakNo)})`;
                    return `Segment ${this.toDisplayString(segmentId)} -> Oda ${this.toDisplayString(odaId)}${yatakText}`;
                });

            rows.push({
                siraNo: this.toDisplayString(siraNo),
                adSoyad: this.toDisplayString(adSoyad),
                cinsiyet: this.toDisplayString(cinsiyet),
                katilimDurumu: this.toDisplayString(katilimDurumu),
                tcKimlikNo: this.toDisplayString(tcKimlikNo),
                pasaportNo: this.toDisplayString(pasaportNo),
                atamalar: atamaOzetleri.length > 0 ? atamaOzetleri.join('\n') : '-'
            });
        }

        return {
            columns: [
                { field: 'siraNo', header: 'Sıra' },
                { field: 'adSoyad', header: 'Ad Soyad' },
                { field: 'cinsiyet', header: 'Cinsiyet' },
                { field: 'katilimDurumu', header: 'Katilim Durumu' },
                { field: 'tcKimlikNo', header: 'TC Kimlik No' },
                { field: 'pasaportNo', header: 'Pasaport No' },
                { field: 'atamalar', header: 'Atamalar' }
            ],
            rows
        };
    }

    private tryBuildGenericTableData(payload: unknown): DegisiklikPayloadTableData | null {
        if (Array.isArray(payload)) {
            if (payload.length === 0) {
                return {
                    columns: [{ field: 'bilgi', header: 'Bilgi' }],
                    rows: [{ bilgi: 'Kayıt yok' }]
                };
            }

            const objectItems = payload.filter((x): x is Record<string, unknown> => !!x && typeof x === 'object' && !Array.isArray(x));
            if (objectItems.length === payload.length) {
                const keys = Array.from(
                    new Set(objectItems.flatMap((x) => Object.keys(x)))
                );

                return {
                    columns: keys.map((key) => ({ field: key, header: this.toDisplayHeader(key) })),
                    rows: objectItems.map((item) => {
                        const row: Record<string, string> = {};
                        for (const key of keys) {
                            row[key] = this.toDisplayString(item[key]);
                        }
                        return row;
                    })
                };
            }

            return {
                columns: [{ field: 'deger', header: 'Deger' }],
                rows: payload.map((x) => ({ deger: this.toDisplayString(x) }))
            };
        }

        if (payload && typeof payload === 'object') {
            const obj = payload as Record<string, unknown>;
            const rows = Object.keys(obj).map((key) => ({
                alan: this.toDisplayHeader(key),
                deger: this.toDisplayString(obj[key])
            }));

            return {
                columns: [
                    { field: 'alan', header: 'Alan' },
                    { field: 'deger', header: 'Deger' }
                ],
                rows
            };
        }

        if (typeof payload !== 'undefined' && payload !== null) {
            return {
                columns: [{ field: 'deger', header: 'Deger' }],
                rows: [{ deger: this.toDisplayString(payload) }]
            };
        }

        return null;
    }

    private readUnknown(source: Record<string, unknown>, keys: string[]): unknown {
        for (const key of keys) {
            if (Object.prototype.hasOwnProperty.call(source, key)) {
                return source[key];
            }
        }

        return undefined;
    }

    private toDisplayHeader(value: string): string {
        return value
            .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
            .replace(/_/g, ' ')
            .replace(/^\w/, (x) => x.toUpperCase());
    }

    private toDisplayString(value: unknown): string {
        if (value === null || typeof value === 'undefined') {
            return '-';
        }

        if (typeof value === 'string') {
            return value.trim().length > 0 ? value : '-';
        }

        if (typeof value === 'number' || typeof value === 'boolean') {
            return String(value);
        }

        if (Array.isArray(value)) {
            if (value.length === 0) {
                return '-';
            }

            const primitiveArray = value.every((x) => x === null || ['string', 'number', 'boolean'].includes(typeof x));
            if (primitiveArray) {
                return value.map((x) => this.toDisplayString(x)).join(', ');
            }

            return `${value.length} kayıt`;
        }

        if (typeof value === 'object') {
            return JSON.stringify(value);
        }

        return String(value);
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

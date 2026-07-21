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
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { tryReadApiMessage } from '../../../../core/api';
import { UiSeverity } from '../../../../core/ui/ui-severity.constants';
import {
    RezervasyonKonaklayanKisiDto,
    RezervasyonKonaklayanOdaSecenekDto,
    RezervasyonKonaklayanPlanDto,
    RezervasyonKonaklayanSegmentDto
} from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';
import { KonaklayanCinsiyetleri } from '../../konaklayan-cinsiyetleri.constants';
import { KonaklayanKatilimDurumlari } from '../../konaklayan-katilim-durumlari.constants';

interface KonaklayanOdaSecenekOption {
    value: number;
    label: string;
    odaNo: string;
    binaAdi: string;
    odaTipiAdi: string;
    ayrilanKisiSayisi: number;
    paylasimliMi: boolean;
}

@Component({
    selector: 'app-rezervasyon-konaklayan-plani-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TagModule],
    templateUrl: './rezervasyon-konaklayan-plani-dialog.html',
    styleUrl: './rezervasyon-konaklayan-plani-dialog.scss'
})
export class RezervasyonKonaklayanPlaniDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() saved = new EventEmitter<void>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    konaklayanPlan: RezervasyonKonaklayanPlanDto | null = null;
    private expandedSiraNolari = new Set<number>();

    private loadSeq = 0;

    readonly cinsiyetSecenekleri = [
        { label: 'Kadin', value: KonaklayanCinsiyetleri.Kadin },
        { label: 'Erkek', value: KonaklayanCinsiyetleri.Erkek }
    ];

    readonly katilimDurumuSecenekleri = [
        { label: 'Bekleniyor', value: KonaklayanKatilimDurumlari.Bekleniyor },
        { label: 'Geldi', value: KonaklayanKatilimDurumlari.Geldi },
        { label: 'Gelmedi', value: KonaklayanKatilimDurumlari.Gelmedi },
        { label: 'Ayrildi', value: KonaklayanKatilimDurumlari.Ayrildi }
    ];

    ngOnChanges(changes: SimpleChanges): void {
        const shouldLoad =
            this.visible &&
            !!this.rezervasyonId &&
            (changes['visible'] || changes['rezervasyonId']);

        if (shouldLoad) {
            this.resetPlanState();
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
        if (!this.rezervasyonId || !this.konaklayanPlan) {
            return;
        }

        this.saving = true;
        this.service
            .saveKonaklayanPlani(this.rezervasyonId, {
                konaklayanlar: this.konaklayanPlan.konaklayanlar.map((kisi) => ({
                    siraNo: kisi.siraNo,
                    adSoyad: (kisi.adSoyad ?? '').trim(),
                    tcKimlikNo: this.normalizeOptional(kisi.tcKimlikNo ?? ''),
                    pasaportNo: this.normalizeOptional(kisi.pasaportNo ?? ''),
                    cinsiyet: this.normalizeOptional(kisi.cinsiyet ?? ''),
                    katilimDurumu: this.normalizeOptional(kisi.katilimDurumu ?? '') ?? KonaklayanKatilimDurumlari.Bekleniyor,
                    atamalar: kisi.atamalar.map((atama) => ({
                        segmentId: atama.segmentId,
                        odaId: atama.odaId,
                        yatakNo: atama.yatakNo
                    }))
                }))
            })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (plan) => {
                    this.konaklayanPlan = plan;
                    this.initializeExpandedState(plan);
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Konaklayan plani kaydedildi.' });
                    this.saved.emit();
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    isKonaklayanExpanded(siraNo: number): boolean {
        return this.expandedSiraNolari.has(siraNo);
    }

    toggleKonaklayanCard(siraNo: number): void {
        if (this.expandedSiraNolari.has(siraNo)) {
            this.expandedSiraNolari.delete(siraNo);
        } else {
            this.expandedSiraNolari.add(siraNo);
        }
    }

    expandAllKonaklayan(): void {
        for (const kisi of this.konaklayanPlan?.konaklayanlar ?? []) {
            this.expandedSiraNolari.add(kisi.siraNo);
        }
    }

    collapseAllKonaklayan(): void {
        this.expandedSiraNolari.clear();
    }

    isKonaklayanComplete(kisi: RezervasyonKonaklayanKisiDto): boolean {
        if ((kisi.adSoyad ?? '').trim().length === 0) {
            return false;
        }

        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return true;
        }

        return this.getKonaklayanSegmentler().every((segment) => {
            const odaId = this.getKonaklayanAtamaOdaId(kisi, segment.segmentId);
            if (!odaId || odaId <= 0) {
                return false;
            }

            if (this.isKonaklayanYatakSecimiGerekli(kisi, segment.segmentId)) {
                const yatakNo = this.getKonaklayanAtamaYatakNo(kisi, segment.segmentId);
                return !!yatakNo && yatakNo > 0;
            }

            return true;
        });
    }

    getKonaklayanSegmentler(): RezervasyonKonaklayanSegmentDto[] {
        return this.konaklayanPlan?.segmentler ?? [];
    }

    getKonaklayanOdaSecenekleri(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): KonaklayanOdaSecenekOption[] {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return [];
        }

        const segment = this.konaklayanPlan?.segmentler.find((x) => x.segmentId === segmentId);
        if (!segment) {
            return [];
        }

        const currentSelection = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        return segment.odaSecenekleri
            .filter((oda) => {
                const selectedCount = this.getSegmentOdaSelectedCount(segmentId, oda.odaId);
                if (currentSelection === oda.odaId) {
                    return true;
                }

                return selectedCount < oda.ayrilanKisiSayisi;
            })
            .map((oda) => ({
                value: oda.odaId,
                label: `${oda.odaNo} - ${oda.binaAdi} (${oda.odaTipiAdi}, ${oda.ayrilanKisiSayisi} kisi, ${oda.paylasimliMi ? 'paylasimli' : 'paylasimsiz'})`,
                odaNo: oda.odaNo,
                binaAdi: oda.binaAdi,
                odaTipiAdi: oda.odaTipiAdi,
                ayrilanKisiSayisi: oda.ayrilanKisiSayisi,
                paylasimliMi: oda.paylasimliMi
            }));
    }

    getKonaklayanYatakSecenekleri(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): { label: string; value: number }[] {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return [];
        }

        const odaId = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        if (!odaId || odaId <= 0) {
            return [];
        }

        const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
        if (!odaSecenegi || !odaSecenegi.paylasimliMi || odaSecenegi.ayrilanKisiSayisi <= 0) {
            return [];
        }

        const currentYatakNo = this.getKonaklayanAtamaYatakNo(kisi, segmentId);
        const selectedBeds = this.getSegmentOdaSelectedBeds(segmentId, odaId);
        const options: { label: string; value: number }[] = [];
        for (let bedNo = 1; bedNo <= odaSecenegi.ayrilanKisiSayisi; bedNo++) {
            if (currentYatakNo === bedNo || !selectedBeds.has(bedNo)) {
                options.push({ label: `Yatak ${bedNo}`, value: bedNo });
            }
        }

        return options;
    }

    getKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): number | null {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        return atama?.odaId ?? null;
    }

    getKonaklayanAtamaYatakNo(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): number | null {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        return atama?.yatakNo ?? null;
    }

    setKonaklayanAtamaOdaId(kisi: RezervasyonKonaklayanKisiDto, segmentId: number, odaId: number | null): void {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        if (atama) {
            atama.odaId = odaId;
            const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
            if (!odaSecenegi || !odaSecenegi.paylasimliMi) {
                atama.yatakNo = null;
                return;
            }

            if ((atama.yatakNo ?? 0) <= 0 || (atama.yatakNo ?? 0) > odaSecenegi.ayrilanKisiSayisi) {
                atama.yatakNo = null;
            }
            return;
        }

        kisi.atamalar = [...kisi.atamalar, { segmentId, odaId, yatakNo: null }];
    }

    setKonaklayanAtamaYatakNo(kisi: RezervasyonKonaklayanKisiDto, segmentId: number, yatakNo: number | null): void {
        const atama = kisi.atamalar.find((x) => x.segmentId === segmentId);
        if (atama) {
            atama.yatakNo = yatakNo;
            return;
        }

        kisi.atamalar = [...kisi.atamalar, { segmentId, odaId: null, yatakNo }];
    }

    setKonaklayanKatilimDurumu(kisi: RezervasyonKonaklayanKisiDto, katilimDurumu: string | null): void {
        kisi.katilimDurumu = katilimDurumu ?? KonaklayanKatilimDurumlari.Bekleniyor;
        if (kisi.katilimDurumu === KonaklayanKatilimDurumlari.Gelmedi) {
            kisi.atamalar = kisi.atamalar.map((x) => ({ ...x, odaId: null, yatakNo: null }));
        }
    }

    isKonaklayanAssignmentsRequired(kisi: RezervasyonKonaklayanKisiDto): boolean {
        return kisi.katilimDurumu !== KonaklayanKatilimDurumlari.Gelmedi;
    }

    isKonaklayanYatakSecimiGerekli(kisi: RezervasyonKonaklayanKisiDto, segmentId: number): boolean {
        if (!this.isKonaklayanAssignmentsRequired(kisi)) {
            return false;
        }

        const odaId = this.getKonaklayanAtamaOdaId(kisi, segmentId);
        if (!odaId || odaId <= 0) {
            return false;
        }

        const odaSecenegi = this.getKonaklayanSegmentOdaSecenegi(segmentId, odaId);
        return !!odaSecenegi?.paylasimliMi;
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
            .getKonaklayanPlani(rezervasyonId)
            .pipe(
                finalize(() => {
                    if (seq === this.loadSeq) {
                        this.loading = false;
                        this.cdr.markForCheck();
                    }
                })
            )
            .subscribe({
                next: (plan) => {
                    if (seq === this.loadSeq) {
                        this.konaklayanPlan = plan;
                        this.initializeExpandedState(plan);
                        this.cdr.markForCheck();
                    }
                },
                error: (error: unknown) => {
                    if (seq === this.loadSeq) {
                        this.konaklayanPlan = null;
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
        this.konaklayanPlan = null;
        this.expandedSiraNolari.clear();
    }

    private resetPlanState(): void {
        this.konaklayanPlan = null;
        this.expandedSiraNolari.clear();
    }

    private initializeExpandedState(plan: RezervasyonKonaklayanPlanDto): void {
        this.expandedSiraNolari = new Set(
            plan.konaklayanlar.filter((kisi) => !this.isKonaklayanComplete(kisi)).map((kisi) => kisi.siraNo)
        );
    }

    private getKonaklayanSegmentOdaSecenegi(segmentId: number, odaId: number | null | undefined): RezervasyonKonaklayanOdaSecenekDto | null {
        if (!odaId || odaId <= 0 || !this.konaklayanPlan) {
            return null;
        }

        const segment = this.konaklayanPlan.segmentler.find((x) => x.segmentId === segmentId);
        if (!segment) {
            return null;
        }

        return segment.odaSecenekleri.find((x) => x.odaId === odaId) ?? null;
    }

    private getSegmentOdaSelectedCount(segmentId: number, odaId: number): number {
        if (!this.konaklayanPlan) {
            return 0;
        }

        return this.konaklayanPlan.konaklayanlar.reduce((total, kisi) => {
            if (!this.isKonaklayanAssignmentsRequired(kisi)) {
                return total;
            }

            const selectedOdaId = kisi.atamalar.find((x) => x.segmentId === segmentId)?.odaId;
            return total + (selectedOdaId === odaId ? 1 : 0);
        }, 0);
    }

    private getSegmentOdaSelectedBeds(segmentId: number, odaId: number): Set<number> {
        if (!this.konaklayanPlan) {
            return new Set<number>();
        }

        const selected = this.konaklayanPlan.konaklayanlar
            .filter((kisi) => this.isKonaklayanAssignmentsRequired(kisi))
            .map((kisi) => kisi.atamalar.find((x) => x.segmentId === segmentId))
            .filter((atama) => atama?.odaId === odaId && (atama?.yatakNo ?? 0) > 0)
            .map((atama) => atama!.yatakNo!) as number[];
        return new Set(selected);
    }

    private normalizeOptional(value: string): string | null {
        const normalized = value.trim();
        return normalized.length > 0 ? normalized : null;
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

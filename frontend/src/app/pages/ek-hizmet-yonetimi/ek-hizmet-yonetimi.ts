import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { EkHizmetDto, EkHizmetFormRow, EkHizmetTarifeDto, EkHizmetTanimFormRow, EkHizmetTesisDto } from './ek-hizmet-yonetimi.dto';
import { EkHizmetYonetimiService } from './ek-hizmet-yonetimi.service';

@Component({
    selector: 'app-ek-hizmet-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, TextareaModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './ek-hizmet-yonetimi.html',
    styles: [`
        :host ::ng-deep .ek-hizmet-table .p-datatable-table {
            min-width: 100%;
        }

        :host ::ng-deep .ek-hizmet-table .p-datatable-thead > tr > th,
        :host ::ng-deep .ek-hizmet-table .p-datatable-tbody > tr > td {
            vertical-align: top;
        }

        :host ::ng-deep .ek-hizmet-table .cell-input,
        :host ::ng-deep .ek-hizmet-table .cell-select {
            width: 100%;
            min-width: 0;
        }

        :host ::ng-deep .ek-hizmet-table .cell-select .p-select {
            width: 100%;
        }

        :host ::ng-deep .ek-hizmet-table .compact-help {
            line-height: 1.2;
            word-break: break-word;
        }
    `],
    providers: [MessageService]
})
export class EkHizmetYonetimi implements OnInit {
    private readonly service = inject(EkHizmetYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: EkHizmetTesisDto[] = [];
    hizmetler: EkHizmetTanimFormRow[] = [];
    tarifeler: EkHizmetFormRow[] = [];
    selectedTesisId: number | null = null;
    loadingReferences = false;
    loadingTesisData = false;
    savingHizmetler = false;
    savingTarifeler = false;
    hizmetPanelExpanded = true;
    tarifePanelExpanded = true;

    get canManage(): boolean {
        return this.authService.hasPermission('EkHizmetYonetimi.Manage');
    }

    get hasTesisSelection(): boolean {
        return !!this.selectedTesisId;
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        this.loadReferences();
    }

    onTesisChange(): void {
        this.loadTesisData();
    }

    toggleHizmetPanel(): void {
        this.hizmetPanelExpanded = !this.hizmetPanelExpanded;
    }

    toggleTarifePanel(): void {
        this.tarifePanelExpanded = !this.tarifePanelExpanded;
    }

    addHizmetRow(): void {
        if (!this.canManage) {
            return;
        }

        this.hizmetPanelExpanded = true;
        this.hizmetler = [
            ...this.hizmetler,
            {
                ad: '',
                aciklama: null,
                birimAdi: 'Adet',
                aktifMi: true
            }
        ];
    }

    removeHizmetRow(index: number): void {
        if (!this.canManage) {
            return;
        }

        this.hizmetler = this.hizmetler.filter((_, i) => i !== index);
    }

    addTarifeRow(): void {
        if (!this.canManage) {
            return;
        }

        this.tarifePanelExpanded = true;
        const today = this.todayInput();
        this.tarifeler = [
            ...this.tarifeler,
            {
                ekHizmetId: this.hizmetSecenekleri[0]?.id ?? null,
                birimFiyat: 0,
                paraBirimi: 'TRY',
                baslangicTarihi: today,
                bitisTarihi: today,
                aktifMi: true
            }
        ];
    }

    removeTarifeRow(index: number): void {
        if (!this.canManage) {
            return;
        }

        this.tarifeler = this.tarifeler.filter((_, i) => i !== index);
    }

    saveHizmetler(): void {
        if (!this.canManage || this.savingHizmetler || !this.selectedTesisId) {
            return;
        }

        const validationError = this.validateHizmetRows();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.hizmetler.map((row) => ({
            id: row.id ?? null,
            tesisId: this.selectedTesisId!,
            ad: row.ad.trim(),
            aciklama: this.normalizeOptional(row.aciklama),
            birimAdi: row.birimAdi.trim(),
            aktifMi: row.aktifMi
        })) satisfies EkHizmetDto[];

        this.savingHizmetler = true;
        this.service
            .upsertHizmetler(this.selectedTesisId, payload)
            .pipe(
                finalize(() => {
                    this.savingHizmetler = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.hizmetler = items.map((item) => this.toHizmetFormRow(item));
                    this.reconcileTarifeSelections();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Ek hizmet tanimlari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    saveTarifeler(): void {
        if (!this.canManage || this.savingTarifeler || !this.selectedTesisId) {
            return;
        }

        if (this.hizmetSecenekleri.length === 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Once en az bir ek hizmet tanimi kaydetmelisiniz.' });
            return;
        }

        const validationError = this.validateTarifeRows();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.tarifeler.map((row) => ({
            id: row.id ?? null,
            tesisId: this.selectedTesisId!,
            ekHizmetId: row.ekHizmetId!,
            ekHizmetAdi: '',
            ekHizmetAciklama: null,
            birimAdi: '',
            birimFiyat: row.birimFiyat ?? 0,
            paraBirimi: (row.paraBirimi || 'TRY').trim().toUpperCase(),
            baslangicTarihi: this.toIsoDate(row.baslangicTarihi),
            bitisTarihi: this.toIsoDate(row.bitisTarihi),
            aktifMi: row.aktifMi
        })) satisfies EkHizmetTarifeDto[];

        this.savingTarifeler = true;
        this.service
            .upsertTarifeler(this.selectedTesisId, payload)
            .pipe(
                finalize(() => {
                    this.savingTarifeler = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.tarifeler = items.map((item) => this.toTarifeFormRow(item));
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Ek hizmet tarifeleri kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    trackByIndex(index: number): number {
        return index;
    }

    get hizmetSecenekleri(): EkHizmetDto[] {
        return this.hizmetler
            .map((row) => ({
                id: row.id ?? null,
                tesisId: this.selectedTesisId ?? 0,
                ad: row.ad,
                aciklama: row.aciklama,
                birimAdi: row.birimAdi,
                aktifMi: row.aktifMi
            }))
            .filter((row) => !!row.id && !!row.ad?.trim())
            .sort((a, b) => a.ad.localeCompare(b.ad));
    }

    getTarifeHizmetLabel(row: EkHizmetFormRow): string {
        const hizmet = this.hizmetSecenekleri.find((item) => item.id === row.ekHizmetId);
        if (!hizmet) {
            return '-';
        }

        return `${hizmet.ad} (${hizmet.birimAdi})`;
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
                next: (items) => {
                    this.tesisler = [...items].sort((a, b) => a.ad.localeCompare(b.ad));
                    if (!this.selectedTesisId || !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = this.tesisler[0]?.id ?? null;
                    }

                    this.loadTesisData();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadTesisData(): void {
        if (!this.selectedTesisId) {
            this.hizmetler = [];
            this.tarifeler = [];
            return;
        }

        this.loadingTesisData = true;
        forkJoin({
            hizmetler: this.service.getHizmetlerByTesis(this.selectedTesisId),
            tarifeler: this.service.getTarifelerByTesis(this.selectedTesisId)
        })
            .pipe(
                finalize(() => {
                    this.loadingTesisData = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ hizmetler, tarifeler }) => {
                    this.hizmetler = hizmetler.map((item) => this.toHizmetFormRow(item));
                    this.tarifeler = tarifeler.map((item) => this.toTarifeFormRow(item));
                    this.reconcileTarifeSelections();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.hizmetler = [];
                    this.tarifeler = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private reconcileTarifeSelections(): void {
        const validIds = this.hizmetSecenekleri.map((x) => x.id).filter((id): id is number => !!id);
        this.tarifeler = this.tarifeler.map((row) => ({
            ...row,
            ekHizmetId: row.ekHizmetId && validIds.includes(row.ekHizmetId) ? row.ekHizmetId : null
        }));
    }

    private toHizmetFormRow(dto: EkHizmetDto): EkHizmetTanimFormRow {
        return {
            id: dto.id ?? null,
            ad: dto.ad,
            aciklama: dto.aciklama,
            birimAdi: dto.birimAdi,
            aktifMi: dto.aktifMi
        };
    }

    private toTarifeFormRow(dto: EkHizmetTarifeDto): EkHizmetFormRow {
        return {
            id: dto.id ?? null,
            ekHizmetId: dto.ekHizmetId,
            birimFiyat: dto.birimFiyat,
            paraBirimi: dto.paraBirimi,
            baslangicTarihi: this.normalizeDateInput(dto.baslangicTarihi),
            bitisTarihi: this.normalizeDateInput(dto.bitisTarihi),
            aktifMi: dto.aktifMi
        };
    }

    private validateHizmetRows(): string | null {
        const names = new Set<string>();

        for (let i = 0; i < this.hizmetler.length; i++) {
            const row = this.hizmetler[i];
            const lineNo = i + 1;
            const normalizedName = row.ad?.trim().toLocaleLowerCase('tr-TR');

            if (!row.ad?.trim()) {
                return `${lineNo}. hizmet satiri: Hizmet adi zorunludur.`;
            }

            if (!row.birimAdi?.trim()) {
                return `${lineNo}. hizmet satiri: Birim adi zorunludur.`;
            }

            if (normalizedName && names.has(normalizedName)) {
                return `${lineNo}. hizmet satiri: Ayni isimle birden fazla hizmet tanimi olamaz.`;
            }

            if (normalizedName) {
                names.add(normalizedName);
            }
        }

        return null;
    }

    private validateTarifeRows(): string | null {
        const groupedRows = new Map<number, Array<{ start: number; end: number; lineNo: number }>>();

        for (let i = 0; i < this.tarifeler.length; i++) {
            const row = this.tarifeler[i];
            const lineNo = i + 1;

            if (!row.ekHizmetId || row.ekHizmetId <= 0) {
                return `${lineNo}. tarife satiri: Gecerli bir ek hizmet seciniz.`;
            }

            if (row.birimFiyat === null || Number.isNaN(Number(row.birimFiyat)) || Number(row.birimFiyat) < 0) {
                return `${lineNo}. tarife satiri: Gecerli bir birim fiyat giriniz.`;
            }

            if (!row.baslangicTarihi || !row.bitisTarihi) {
                return `${lineNo}. tarife satiri: Baslangic ve bitis tarihleri zorunludur.`;
            }

            const start = new Date(row.baslangicTarihi).getTime();
            const end = new Date(row.bitisTarihi).getTime();
            if (start > end) {
                return `${lineNo}. tarife satiri: Baslangic tarihi bitis tarihinden buyuk olamaz.`;
            }

            const rows = groupedRows.get(row.ekHizmetId) ?? [];
            rows.push({ start, end, lineNo });
            groupedRows.set(row.ekHizmetId, rows);
        }

        for (const [, rows] of groupedRows) {
            rows.sort((a, b) => a.start - b.start || a.end - b.end);
            for (let i = 1; i < rows.length; i++) {
                if (rows[i].start <= rows[i - 1].end) {
                    return `${rows[i].lineNo}. tarife satiri: Ayni hizmet icin cakisan tarih araligi tanimlanamaz.`;
                }
            }
        }

        return null;
    }

    private normalizeDateInput(value: string): string {
        return value ? value.slice(0, 10) : this.todayInput();
    }

    private toIsoDate(value: string): string {
        return new Date(value).toISOString();
    }

    private todayInput(): string {
        return new Date().toISOString().slice(0, 10);
    }

    private normalizeOptional(value: string | null | undefined): string | null {
        const normalized = value?.trim();
        return normalized ? normalized : null;
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof Error && error.message) {
            return error.message;
        }

        return tryReadApiMessage(error) ?? 'Beklenmeyen bir hata olustu.';
    }
}

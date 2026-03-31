import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { getOdaFiyatKullanimSekliLabel, ODA_FIYAT_KULLANIM_SEKLI_OPTIONS } from './oda-fiyat-kullanim-sekli.constants';
import { KonaklamaTipiDto, MisafirTipiDto, OdaFiyatDto, OdaFiyatFormRow, OdaTipiDto } from './oda-fiyat-yonetimi.dto';
import { OdaFiyatYonetimiService } from './oda-fiyat-yonetimi.service';

@Component({
    selector: 'app-oda-fiyat-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule, TagModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './oda-fiyat-yonetimi.html',
    providers: [MessageService]
})
export class OdaFiyatYonetimi implements OnInit {
    private readonly service = inject(OdaFiyatYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: TesisDto[] = [];
    odaTipleri: OdaTipiDto[] = [];
    konaklamaTipleri: KonaklamaTipiDto[] = [];
    misafirTipleri: MisafirTipiDto[] = [];
    fiyatSatirlari: OdaFiyatFormRow[] = [];
    readonly kullanimSekliSecenekleri = ODA_FIYAT_KULLANIM_SEKLI_OPTIONS;

    selectedTesisId: number | null = null;
    selectedOdaTipiId: number | null = null;
    showOnlyIncompleteRows = false;
    showOnlyZeroPriceRows = false;
    loadingReferences = false;
    loadingFiyatlar = false;
    saving = false;

    get canManage(): boolean {
        return this.authService.hasPermission('OdaFiyatYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        this.loadReferences();
    }

    onOdaTipiChange(): void {
        this.loadFiyatlarForSelectedOdaTipi();
    }

    onTesisChange(): void {
        this.applySelectedOdaTipiForSelectedTesis();
        this.loadMisafirTipleriForSelectedTesis();
        this.loadKonaklamaTipleriForSelectedTesis();
        this.loadFiyatlarForSelectedOdaTipi();
    }

    get filteredOdaTipleri(): OdaTipiDto[] {
        if (!this.selectedTesisId) {
            return this.odaTipleri;
        }

        return this.odaTipleri.filter((x) => x.tesisId === this.selectedTesisId);
    }

    get visibleFiyatSatirlari(): OdaFiyatFormRow[] {
        return this.fiyatSatirlari
            .map((row, index) => ({ row, index }))
            .filter(({ row }) => {
                if (this.showOnlyIncompleteRows && !this.isIncompleteCombination(row)) {
                    return false;
                }

                if (this.showOnlyZeroPriceRows && !this.isZeroPriceRow(row)) {
                    return false;
                }

                return true;
            })
            .sort((left, right) => {
                const leftPriority = left.row.uiYeniOzelKullanimSatiriMi ? 0 : 1;
                const rightPriority = right.row.uiYeniOzelKullanimSatiriMi ? 0 : 1;
                if (leftPriority !== rightPriority) {
                    return leftPriority - rightPriority;
                }

                return left.index - right.index;
            })
            .map((item) => item.row);
    }

    get selectedOdaTipi(): OdaTipiDto | null {
        return this.filteredOdaTipleri.find((x) => x.id === this.selectedOdaTipiId) ?? null;
    }

    get fiyatlamaBilgiMesaji(): string | null {
        const odaTipi = this.selectedOdaTipi;
        if (!odaTipi) {
            return null;
        }

        if (odaTipi.paylasimliMi) {
            return 'Bu oda tipi paylasimli. Varsayilan fiyatlama kisi basi calisir. Ozel kullanim satiri tanimlasaniz bile senaryo hesaplamasinda kullanilmaz.';
        }

        return 'Paylasimsiz odalarda ayni kombinasyon icin hem Kisi Basi hem Ozel Kullanim tarife tanimlayabilirsiniz. Sistem once Ozel Kullanim fiyatini kullanir; yoksa kisi basi fiyata duser.';
    }

    get eksikOzelKullanimUyarilari(): string[] {
        const odaTipi = this.selectedOdaTipi;
        if (!odaTipi || odaTipi.paylasimliMi) {
            return [];
        }

        const konaklamaTipiMap = new Map(this.konaklamaTipleri.map((item) => [item.id, item.ad]));
        const misafirTipiMap = new Map(this.misafirTipleri.map((item) => [item.id, item.ad]));
        const warnings: string[] = [];

        const groups = new Map<string, { konaklamaTipiId: number | null; misafirTipiId: number | null; hasKisiBasi: boolean; hasOzelKullanim: boolean }>();
        for (const row of this.fiyatSatirlari) {
            const key = `${row.konaklamaTipiId ?? 0}-${row.misafirTipiId ?? 0}`;
            const existing = groups.get(key) ?? {
                konaklamaTipiId: row.konaklamaTipiId,
                misafirTipiId: row.misafirTipiId,
                hasKisiBasi: false,
                hasOzelKullanim: false
            };

            if (row.kullanimSekli === 'KisiBasi') {
                existing.hasKisiBasi = true;
            }

            if (row.kullanimSekli === 'OzelKullanim') {
                existing.hasOzelKullanim = true;
            }

            groups.set(key, existing);
        }

        for (const group of groups.values()) {
            if (group.hasKisiBasi && !group.hasOzelKullanim) {
                const konaklamaTipiAdi = group.konaklamaTipiId ? (konaklamaTipiMap.get(group.konaklamaTipiId) ?? 'Bilinmeyen Konaklama Tipi') : 'Konaklama tipi secilmemis';
                const misafirTipiAdi = group.misafirTipiId ? (misafirTipiMap.get(group.misafirTipiId) ?? 'Bilinmeyen Misafir Tipi') : 'Misafir tipi secilmemis';
                warnings.push(`${konaklamaTipiAdi} / ${misafirTipiAdi}: Ozel kullanim tarifesi eksik.`);
            }
        }

        return warnings;
    }

    addRow(): void {
        if (!this.canManage) {
            return;
        }

        const defaultKonaklamaTipiId = this.konaklamaTipleri[0]?.id ?? null;
        const defaultMisafirTipiId = this.misafirTipleri[0]?.id ?? null;
        const today = this.todayInput();

        this.fiyatSatirlari = [
            ...this.fiyatSatirlari,
            {
                konaklamaTipiId: defaultKonaklamaTipiId,
                misafirTipiId: defaultMisafirTipiId,
                kullanimSekli: this.selectedOdaTipi?.paylasimliMi ? 'KisiBasi' : 'OzelKullanim',
                fiyat: 0,
                paraBirimi: 'TRY',
                baslangicTarihi: today,
                bitisTarihi: today,
                aktifMi: true
            }
        ];
    }

    addMissingRows(): void {
        this.addMissingRowsByModes(this.selectedOdaTipi?.paylasimliMi ? ['KisiBasi'] : ['KisiBasi', 'OzelKullanim']);
    }

    addMissingOzelKullanimRows(): void {
        this.addMissingRowsByModes(['OzelKullanim'], true);
    }

    private addMissingRowsByModes(modes: string[], markAsNewOzelKullanim = false): void {
        if (!this.canManage || !this.selectedOdaTipi) {
            return;
        }

        if (this.konaklamaTipleri.length === 0 || this.misafirTipleri.length === 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Veri', detail: 'Eksik satirlari olusturmak icin once konaklama ve misafir tipleri yuklenmelidir.' });
            return;
        }

        const usageModes = this.selectedOdaTipi.paylasimliMi
            ? modes.filter((mode) => mode === 'KisiBasi')
            : modes;

        if (usageModes.length === 0) {
            this.messageService.add({ severity: UiSeverity.Info, summary: 'Uygun Degil', detail: 'Paylasimli oda tiplerinde ozel kullanim satiri olusturulmaz.' });
            return;
        }

        const existingKeys = new Set(
            this.fiyatSatirlari.map((row) => `${row.konaklamaTipiId ?? 0}-${row.misafirTipiId ?? 0}-${row.kullanimSekli}`)
        );

        const defaultDate = this.todayInput();
        const newRows: OdaFiyatFormRow[] = [];

        for (const konaklamaTipi of this.konaklamaTipleri) {
            for (const misafirTipi of this.misafirTipleri) {
                const siblingRows = this.fiyatSatirlari.filter((row) => row.konaklamaTipiId === konaklamaTipi.id && row.misafirTipiId === misafirTipi.id);
                const templateRow = siblingRows[0] ?? null;

                for (const kullanimSekli of usageModes) {
                    const key = `${konaklamaTipi.id ?? 0}-${misafirTipi.id ?? 0}-${kullanimSekli}`;
                    if (existingKeys.has(key)) {
                        continue;
                    }

                    existingKeys.add(key);
                    newRows.push({
                        konaklamaTipiId: konaklamaTipi.id ?? null,
                        misafirTipiId: misafirTipi.id ?? null,
                        kullanimSekli,
                        fiyat: templateRow?.fiyat ?? 0,
                        paraBirimi: templateRow?.paraBirimi ?? 'TRY',
                        baslangicTarihi: templateRow?.baslangicTarihi ?? defaultDate,
                        bitisTarihi: templateRow?.bitisTarihi ?? defaultDate,
                        aktifMi: templateRow?.aktifMi ?? true,
                        uiYeniOzelKullanimSatiriMi: markAsNewOzelKullanim && kullanimSekli === 'OzelKullanim'
                    });
                }
            }
        }

        if (newRows.length === 0) {
            this.messageService.add({ severity: UiSeverity.Info, summary: 'Tamam', detail: 'Eksik kombinasyon bulunmuyor.' });
            return;
        }

        this.fiyatSatirlari = markAsNewOzelKullanim
            ? [...newRows, ...this.fiyatSatirlari]
            : [...this.fiyatSatirlari, ...newRows];
        this.messageService.add({ severity: UiSeverity.Success, summary: 'Satirlar Olusturuldu', detail: `${newRows.length} eksik fiyat satiri eklendi.` });
    }

    removeRow(index: number): void {
        if (!this.canManage) {
            return;
        }

        this.fiyatSatirlari = this.fiyatSatirlari.filter((_, i) => i !== index);
    }

    save(): void {
        if (!this.canManage || this.saving || !this.selectedOdaTipiId) {
            return;
        }

        const validationError = this.validateRows();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.fiyatSatirlari.map((row) => ({
            id: row.id ?? null,
            tesisOdaTipiId: this.selectedOdaTipiId!,
            konaklamaTipiId: row.konaklamaTipiId!,
            misafirTipiId: row.misafirTipiId!,
            kisiSayisi: 1,
            kullanimSekli: row.kullanimSekli,
            fiyat: row.fiyat!,
            paraBirimi: (row.paraBirimi || 'TRY').trim().toUpperCase(),
            baslangicTarihi: this.toIsoDate(row.baslangicTarihi),
            bitisTarihi: this.toIsoDate(row.bitisTarihi),
            aktifMi: row.aktifMi
        })) satisfies OdaFiyatDto[];

        this.saving = true;
        this.service
            .upsertOdaFiyatlari(this.selectedOdaTipiId, payload)
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.fiyatSatirlari = items.map((item) => this.toFormRow(item));
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Oda fiyatlari kaydedildi.' });
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

    private loadReferences(): void {
        this.loadingReferences = true;
        forkJoin({
            tesisler: this.service.getTesisler(),
            odaTipleri: this.service.getOdaTipleri()
        })
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, odaTipleri }) => {
                    this.tesisler = [...tesisler].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.odaTipleri = [...odaTipleri].sort((a, b) => a.ad.localeCompare(b.ad));

                    if (
                        this.selectedTesisId &&
                        !this.tesisler.some((x) => x.id === this.selectedTesisId)
                    ) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId) {
                        this.selectedTesisId = this.odaTipleri[0]?.tesisId ?? this.tesisler[0]?.id ?? null;
                    }

                    this.applySelectedOdaTipiForSelectedTesis();
                    this.loadMisafirTipleriForSelectedTesis();
                    this.loadKonaklamaTipleriForSelectedTesis();
                    this.loadFiyatlarForSelectedOdaTipi();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadMisafirTipleriForSelectedTesis(): void {
        if (!this.selectedTesisId) {
            this.misafirTipleri = [];
            this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({ ...row, misafirTipiId: null }));
            return;
        }

        this.service.getMisafirTipleri(this.selectedTesisId).subscribe({
            next: (misafirTipleri) => {
                this.misafirTipleri = [...misafirTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                const allowedIds = new Set(this.misafirTipleri.map((x) => x.id).filter((x): x is number => !!x));
                const defaultMisafirTipiId = this.misafirTipleri[0]?.id ?? null;
                this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({
                    ...row,
                    misafirTipiId: row.misafirTipiId && allowedIds.has(row.misafirTipiId) ? row.misafirTipiId : defaultMisafirTipiId
                }));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.misafirTipleri = [];
                this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({ ...row, misafirTipiId: null }));
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                this.cdr.detectChanges();
            }
        });
    }

    private loadKonaklamaTipleriForSelectedTesis(): void {
        if (!this.selectedTesisId) {
            this.konaklamaTipleri = [];
            this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({ ...row, konaklamaTipiId: null }));
            return;
        }

        this.service.getKonaklamaTipleri(this.selectedTesisId).subscribe({
            next: (konaklamaTipleri) => {
                this.konaklamaTipleri = [...konaklamaTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                const allowedIds = new Set(this.konaklamaTipleri.map((x) => x.id).filter((x): x is number => !!x));
                const defaultKonaklamaTipiId = this.konaklamaTipleri[0]?.id ?? null;
                this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({
                    ...row,
                    konaklamaTipiId: row.konaklamaTipiId && allowedIds.has(row.konaklamaTipiId) ? row.konaklamaTipiId : defaultKonaklamaTipiId
                }));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.konaklamaTipleri = [];
                this.fiyatSatirlari = this.fiyatSatirlari.map((row) => ({ ...row, konaklamaTipiId: null }));
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                this.cdr.detectChanges();
            }
        });
    }

    private loadFiyatlarForSelectedOdaTipi(): void {
        if (!this.selectedOdaTipiId) {
            this.fiyatSatirlari = [];
            return;
        }

        this.loadingFiyatlar = true;
        this.service
            .getOdaFiyatlariByOdaTipi(this.selectedOdaTipiId)
            .pipe(
                finalize(() => {
                    this.loadingFiyatlar = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.fiyatSatirlari = items.map((item) => this.toFormRow(item));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.fiyatSatirlari = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private applySelectedOdaTipiForSelectedTesis(): void {
        const filteredOdaTipleri = this.filteredOdaTipleri;
        if (filteredOdaTipleri.length === 0) {
            this.selectedOdaTipiId = null;
            this.fiyatSatirlari = [];
            return;
        }

        const hasSelected = filteredOdaTipleri.some((x) => x.id === this.selectedOdaTipiId);
        if (!hasSelected) {
            this.selectedOdaTipiId = filteredOdaTipleri[0].id ?? null;
        }
    }

    private toFormRow(dto: OdaFiyatDto): OdaFiyatFormRow {
        return {
            id: dto.id ?? null,
            konaklamaTipiId: dto.konaklamaTipiId,
            misafirTipiId: dto.misafirTipiId,
            kullanimSekli: dto.kullanimSekli || 'KisiBasi',
            fiyat: dto.fiyat,
            paraBirimi: dto.paraBirimi,
            baslangicTarihi: this.normalizeDateInput(dto.baslangicTarihi),
            bitisTarihi: this.normalizeDateInput(dto.bitisTarihi),
            aktifMi: dto.aktifMi
        };
    }

    private validateRows(): string | null {
        for (let i = 0; i < this.fiyatSatirlari.length; i++) {
            const row = this.fiyatSatirlari[i];
            const lineNo = i + 1;

            if (!row.konaklamaTipiId) {
                return `${lineNo}. satir: Konaklama tipi secimi zorunludur.`;
            }

            if (!row.misafirTipiId) {
                return `${lineNo}. satir: Misafir tipi secimi zorunludur.`;
            }

            if (!row.kullanimSekli) {
                return `${lineNo}. satir: Kullanim sekli secimi zorunludur.`;
            }

            if (row.fiyat === null || row.fiyat < 0) {
                return `${lineNo}. satir: Fiyat sifirdan kucuk olamaz.`;
            }

            if (!row.paraBirimi || row.paraBirimi.trim().length === 0) {
                return `${lineNo}. satir: Para birimi zorunludur.`;
            }

            if (!row.baslangicTarihi || !row.bitisTarihi) {
                return `${lineNo}. satir: Baslangic ve bitis tarihi zorunludur.`;
            }

            if (row.baslangicTarihi > row.bitisTarihi) {
                return `${lineNo}. satir: Baslangic tarihi bitis tarihinden buyuk olamaz.`;
            }
        }

        return null;
    }

    private normalizeDateInput(value: string): string {
        if (!value) {
            return this.todayInput();
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return this.todayInput();
        }

        return date.toISOString().slice(0, 10);
    }

    private toIsoDate(value: string): string {
        return `${value}T00:00:00`;
    }

    private todayInput(): string {
        return new Date().toISOString().slice(0, 10);
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

    getKullanimSekliLabel(value: string | null | undefined): string {
        return getOdaFiyatKullanimSekliLabel(value);
    }

    isZeroPriceRow(row: OdaFiyatFormRow): boolean {
        return Number(row.fiyat ?? 0) === 0;
    }

    getZeroPriceRowMessage(row: OdaFiyatFormRow): string {
        return row.kullanimSekli === 'OzelKullanim'
            ? 'Ozel kullanim fiyatı henuz girilmedi.'
            : 'Kisi basi fiyat henuz girilmedi.';
    }

    isNewOzelKullanimRow(row: OdaFiyatFormRow): boolean {
        return row.uiYeniOzelKullanimSatiriMi === true;
    }

    clearNewOzelKullanimBadge(row: OdaFiyatFormRow): void {
        if (!row.uiYeniOzelKullanimSatiriMi) {
            return;
        }

        row.uiYeniOzelKullanimSatiriMi = false;
    }

    isIncompleteCombination(row: OdaFiyatFormRow): boolean {
        const odaTipi = this.selectedOdaTipi;
        if (!odaTipi || odaTipi.paylasimliMi) {
            return false;
        }

        const siblingRows = this.fiyatSatirlari.filter((item) =>
            item.konaklamaTipiId === row.konaklamaTipiId &&
            item.misafirTipiId === row.misafirTipiId
        );

        const hasKisiBasi = siblingRows.some((item) => item.kullanimSekli === 'KisiBasi');
        const hasOzelKullanim = siblingRows.some((item) => item.kullanimSekli === 'OzelKullanim');
        return !(hasKisiBasi && hasOzelKullanim);
    }
}

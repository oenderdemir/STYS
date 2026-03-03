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
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { KonaklamaTipiDto, MisafirTipiDto, OdaFiyatDto, OdaFiyatFormRow, OdaTipiDto } from './oda-fiyat-yonetimi.dto';
import { OdaFiyatYonetimiService } from './oda-fiyat-yonetimi.service';

@Component({
    selector: 'app-oda-fiyat-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
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

    selectedTesisId: number | null = null;
    selectedOdaTipiId: number | null = null;
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
        this.loadFiyatlarForSelectedOdaTipi();
    }

    get filteredOdaTipleri(): OdaTipiDto[] {
        if (!this.selectedTesisId) {
            return this.odaTipleri;
        }

        return this.odaTipleri.filter((x) => x.tesisId === this.selectedTesisId);
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
                fiyat: 0,
                paraBirimi: 'TRY',
                baslangicTarihi: today,
                bitisTarihi: today,
                aktifMi: true
            }
        ];
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
            this.messageService.add({ severity: 'warn', summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.fiyatSatirlari.map((row) => ({
            id: row.id ?? null,
            tesisOdaTipiId: this.selectedOdaTipiId!,
            konaklamaTipiId: row.konaklamaTipiId!,
            misafirTipiId: row.misafirTipiId!,
            kisiSayisi: 1,
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
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Oda fiyatlari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
            odaTipleri: this.service.getOdaTipleri(),
            konaklamaTipleri: this.service.getKonaklamaTipleri(),
            misafirTipleri: this.service.getMisafirTipleri()
        })
            .pipe(
                finalize(() => {
                    this.loadingReferences = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: ({ tesisler, odaTipleri, konaklamaTipleri, misafirTipleri }) => {
                    this.tesisler = [...tesisler].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.odaTipleri = [...odaTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.konaklamaTipleri = [...konaklamaTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.misafirTipleri = [...misafirTipleri].sort((a, b) => a.ad.localeCompare(b.ad));

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
                    this.loadFiyatlarForSelectedOdaTipi();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
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
}

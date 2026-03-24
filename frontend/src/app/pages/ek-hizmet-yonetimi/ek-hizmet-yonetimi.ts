import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { UiSeverity } from '@/app/core/ui/ui-severity.constants';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { EkHizmetFormRow, EkHizmetTarifeDto, EkHizmetTesisDto } from './ek-hizmet-yonetimi.dto';
import { EkHizmetYonetimiService } from './ek-hizmet-yonetimi.service';

@Component({
    selector: 'app-ek-hizmet-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './ek-hizmet-yonetimi.html',
    providers: [MessageService]
})
export class EkHizmetYonetimi implements OnInit {
    private readonly service = inject(EkHizmetYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: EkHizmetTesisDto[] = [];
    tarifeler: EkHizmetFormRow[] = [];
    selectedTesisId: number | null = null;
    loadingReferences = false;
    loadingTarifeler = false;
    saving = false;

    get canManage(): boolean {
        return this.authService.hasPermission('EkHizmetYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadReferences();
    }

    refresh(): void {
        this.loadReferences();
    }

    onTesisChange(): void {
        this.loadTarifeler();
    }

    addRow(): void {
        if (!this.canManage) {
            return;
        }

        const today = this.todayInput();
        this.tarifeler = [
            ...this.tarifeler,
            {
                ad: '',
                aciklama: null,
                birimAdi: 'Adet',
                birimFiyat: 0,
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

        this.tarifeler = this.tarifeler.filter((_, i) => i !== index);
    }

    save(): void {
        if (!this.canManage || this.saving || !this.selectedTesisId) {
            return;
        }

        const validationError = this.validateRows();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.tarifeler.map((row) => ({
            id: row.id ?? null,
            tesisId: this.selectedTesisId!,
            ad: row.ad.trim(),
            aciklama: this.normalizeOptional(row.aciklama),
            birimAdi: row.birimAdi.trim(),
            birimFiyat: row.birimFiyat ?? 0,
            paraBirimi: (row.paraBirimi || 'TRY').trim().toUpperCase(),
            baslangicTarihi: this.toIsoDate(row.baslangicTarihi),
            bitisTarihi: this.toIsoDate(row.bitisTarihi),
            aktifMi: row.aktifMi
        })) satisfies EkHizmetTarifeDto[];

        this.saving = true;
        this.service
            .upsertTarifeler(this.selectedTesisId, payload)
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.tarifeler = items.map((item) => this.toFormRow(item));
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

                    this.loadTarifeler();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadTarifeler(): void {
        if (!this.selectedTesisId) {
            this.tarifeler = [];
            return;
        }

        this.loadingTarifeler = true;
        this.service
            .getTarifelerByTesis(this.selectedTesisId)
            .pipe(
                finalize(() => {
                    this.loadingTarifeler = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.tarifeler = items.map((item) => this.toFormRow(item));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tarifeler = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private toFormRow(dto: EkHizmetTarifeDto): EkHizmetFormRow {
        return {
            id: dto.id ?? null,
            ad: dto.ad,
            aciklama: dto.aciklama,
            birimAdi: dto.birimAdi,
            birimFiyat: dto.birimFiyat,
            paraBirimi: dto.paraBirimi,
            baslangicTarihi: this.normalizeDateInput(dto.baslangicTarihi),
            bitisTarihi: this.normalizeDateInput(dto.bitisTarihi),
            aktifMi: dto.aktifMi
        };
    }

    private validateRows(): string | null {
        for (let i = 0; i < this.tarifeler.length; i++) {
            const row = this.tarifeler[i];
            const lineNo = i + 1;

            if (!row.ad?.trim()) {
                return `${lineNo}. satir: Hizmet adi zorunludur.`;
            }

            if (!row.birimAdi?.trim()) {
                return `${lineNo}. satir: Birim adi zorunludur.`;
            }

            if (row.birimFiyat === null || Number.isNaN(Number(row.birimFiyat)) || Number(row.birimFiyat) < 0) {
                return `${lineNo}. satir: Gecerli bir birim fiyat giriniz.`;
            }

            if (!row.baslangicTarihi || !row.bitisTarihi) {
                return `${lineNo}. satir: Baslangic ve bitis tarihleri zorunludur.`;
            }

            if (new Date(row.baslangicTarihi).getTime() > new Date(row.bitisTarihi).getTime()) {
                return `${lineNo}. satir: Baslangic tarihi bitis tarihinden buyuk olamaz.`;
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

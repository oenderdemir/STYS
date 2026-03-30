import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { forkJoin } from 'rxjs';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { AuthService } from '../auth';
import { EkHizmetDto, EkHizmetFormRow, EkHizmetTarifeDto, EkHizmetTesisDto } from './ek-hizmet-yonetimi.dto';
import { EkHizmetYonetimiService } from './ek-hizmet-yonetimi.service';

@Component({
    selector: 'app-ek-hizmet-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './ek-hizmet-yonetimi.html',
    styles: [`
        .page-shell { display: flex; flex-direction: column; gap: 1.25rem; }
        .page-card { background: #fff; border: 1px solid #dbe4ee; border-radius: 1rem; padding: 1.25rem; box-shadow: 0 12px 30px rgba(15, 23, 42, 0.04); }
        .page-title { margin: 0; font-size: 1.1rem; font-weight: 700; color: #0f172a; }
        .page-subtitle { margin-top: 0.35rem; color: #64748b; font-size: 0.92rem; line-height: 1.45; }
        .toolbar-links { display: flex; gap: 0.5rem; flex-wrap: wrap; }
        .selection-grid { display: grid; grid-template-columns: minmax(18rem, 24rem) minmax(0, 1fr); gap: 1rem; margin-top: 1rem; align-items: end; }
        .cell-input, .cell-select { width: 100%; }
        .compact-help { line-height: 1.35; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-wrapper { overflow: auto; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-table { width: 100%; }
        :host ::ng-deep .ek-hizmet-table .p-datatable-thead > tr > th { white-space: nowrap; }
        @media (max-width: 991px) { .selection-grid { grid-template-columns: 1fr; } }
    `],
    providers: [MessageService]
})
export class EkHizmetYonetimi implements OnInit {
    private readonly service = inject(EkHizmetYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    tesisler: EkHizmetTesisDto[] = [];
    selectedTesisId: number | null = null;
    tarifeler: EkHizmetFormRow[] = [];
    hizmetSecenekleri: EkHizmetDto[] = [];

    loadingReferences = false;
    loadingTesisData = false;
    savingTarifeler = false;

    get hasTesisSelection(): boolean {
        return !!this.selectedTesisId;
    }

    get canManage(): boolean {
        return this.authService.hasPermission('EkHizmetTarifeYonetimi.Manage');
    }

    get canViewDefinitions(): boolean {
        return this.authService.hasPermission('EkHizmetTanimYonetimi.View');
    }

    get canViewAssignments(): boolean {
        return this.authService.hasPermission('EkHizmetTesisAtamaYonetimi.View');
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

    openDefinitions(): void {
        void this.router.navigate(['/ek-hizmet-tanimlari']);
    }

    openAssignments(): void {
        void this.router.navigate(['/ek-hizmet-atamalari']);
    }

    addTarifeRow(): void {
        if (!this.canManage || !this.selectedTesisId) {
            return;
        }

        if (this.hizmetSecenekleri.length === 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Once ilgili tesis icin en az bir ek hizmet atamasi yapmalisiniz.' });
            return;
        }

        this.tarifeler = [
            ...this.tarifeler,
            {
                ekHizmetId: this.hizmetSecenekleri[0]?.id ?? null,
                birimFiyat: 0,
                paraBirimi: 'TRY',
                baslangicTarihi: '',
                bitisTarihi: '',
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

    saveTarifeler(): void {
        if (!this.canManage || !this.selectedTesisId || this.savingTarifeler) {
            return;
        }

        const validationError = this.validateTarifeler();
        if (validationError) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: validationError });
            return;
        }

        const payload = this.tarifeler.map((row) => ({
            id: row.id ?? null,
            tesisId: this.selectedTesisId!,
            ekHizmetId: row.ekHizmetId,
            ekHizmetAdi: '',
            ekHizmetAciklama: null,
            birimAdi: '',
            birimFiyat: row.birimFiyat ?? 0,
            paraBirimi: (row.paraBirimi || 'TRY').trim().toUpperCase(),
            baslangicTarihi: row.baslangicTarihi,
            bitisTarihi: row.bitisTarihi,
            aktifMi: row.aktifMi
        } as EkHizmetTarifeDto));

        this.savingTarifeler = true;
        this.service
            .upsertTarifeler(this.selectedTesisId, payload)
            .pipe(finalize(() => {
                this.savingTarifeler = false;
                this.cdr.detectChanges();
            }))
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

    getTarifeHizmetLabel(row: EkHizmetFormRow): string {
        const hizmet = this.hizmetSecenekleri.find((item) => item.id === row.ekHizmetId);
        return hizmet ? `${hizmet.ad} (${hizmet.birimAdi})` : '-';
    }

    private loadReferences(): void {
        this.loadingReferences = true;
        this.service
            .getTesisler()
            .pipe(finalize(() => {
                this.loadingReferences = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (tesisler) => {
                    this.tesisler = tesisler;
                    if (this.selectedTesisId && !this.tesisler.some((x) => x.id === this.selectedTesisId)) {
                        this.selectedTesisId = null;
                    }

                    if (!this.selectedTesisId) {
                        this.selectedTesisId = this.tesisler[0]?.id ?? null;
                    }

                    this.loadTesisData();
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.tesisler = [];
                    this.selectedTesisId = null;
                    this.hizmetSecenekleri = [];
                    this.tarifeler = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadTesisData(): void {
        if (!this.selectedTesisId) {
            this.hizmetSecenekleri = [];
            this.tarifeler = [];
            return;
        }

        this.loadingTesisData = true;
        forkJoin({
            hizmetler: this.service.getHizmetlerByTesis(this.selectedTesisId),
            tarifeler: this.service.getTarifelerByTesis(this.selectedTesisId)
        })
            .pipe(finalize(() => {
                this.loadingTesisData = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: ({ hizmetler, tarifeler }) => {
                    this.hizmetSecenekleri = hizmetler;
                    this.tarifeler = tarifeler.map((item) => this.toTarifeFormRow(item));
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.hizmetSecenekleri = [];
                    this.tarifeler = [];
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private toTarifeFormRow(item: EkHizmetTarifeDto): EkHizmetFormRow {
        return {
            id: item.id ?? null,
            ekHizmetId: item.ekHizmetId,
            birimFiyat: item.birimFiyat,
            paraBirimi: item.paraBirimi,
            baslangicTarihi: this.toInputDate(item.baslangicTarihi),
            bitisTarihi: this.toInputDate(item.bitisTarihi),
            aktifMi: item.aktifMi
        };
    }

    private toInputDate(value: string): string {
        return value ? value.slice(0, 10) : '';
    }

    private validateTarifeler(): string | null {
        if (!this.selectedTesisId) {
            return 'Tesis secimi zorunludur.';
        }

        const validIds = this.hizmetSecenekleri.map((x) => x.id).filter((id): id is number => typeof id === 'number');
        const rows = this.tarifeler.map((row, index) => ({ ...row, lineNo: index + 1 }));

        for (const row of rows) {
            if (!row.ekHizmetId || !validIds.includes(row.ekHizmetId)) {
                return `${row.lineNo}. tarife satiri: Gecerli bir ek hizmet seciniz.`;
            }

            if (row.birimFiyat === null || row.birimFiyat < 0) {
                return `${row.lineNo}. tarife satiri: Gecerli bir birim fiyat giriniz.`;
            }

            if (!row.baslangicTarihi || !row.bitisTarihi) {
                return `${row.lineNo}. tarife satiri: Baslangic ve bitis tarihleri zorunludur.`;
            }

            if (row.baslangicTarihi > row.bitisTarihi) {
                return `${row.lineNo}. tarife satiri: Baslangic tarihi bitis tarihinden buyuk olamaz.`;
            }
        }

        const groups = new Map<number, typeof rows>();
        for (const row of rows) {
            const list = groups.get(row.ekHizmetId!) ?? [];
            list.push(row);
            groups.set(row.ekHizmetId!, list);
        }

        for (const rowsForService of groups.values()) {
            const ordered = [...rowsForService].sort((a, b) => a.baslangicTarihi.localeCompare(b.baslangicTarihi) || a.bitisTarihi.localeCompare(b.bitisTarihi));
            for (let i = 1; i < ordered.length; i++) {
                if (ordered[i].baslangicTarihi <= ordered[i - 1].bitisTarihi) {
                    return `${ordered[i].lineNo}. tarife satiri: Ayni hizmet icin cakisan tarih araligi tanimlanamaz.`;
                }
            }
        }

        return null;
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

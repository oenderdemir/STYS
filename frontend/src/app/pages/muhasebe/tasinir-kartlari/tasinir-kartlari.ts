import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { TasinirKodlariService } from '../tasinir-kodlari/tasinir-kodlari.service';
import { MALZEME_TIPLERI, MuhasebeTesisModel, PaketTuruOptionModel, TasinirKartModel } from './tasinir-kartlari.dto';
import { TasinirKartlariService } from './tasinir-kartlari.service';

@Component({
    selector: 'app-tasinir-kartlari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, AutoCompleteModule, InputNumberModule, InputTextModule, SelectModule, CheckboxModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './tasinir-kartlari.html',
    providers: [MessageService, ConfirmationService]
})
export class TasinirKartlariPage implements OnInit {
    private readonly service = inject(TasinirKartlariService);
    private readonly tasinirKodService = inject(TasinirKodlariService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: TasinirKartModel[] = [];
    filteredRecords: TasinirKartModel[] = [];
    model: TasinirKartModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    tasinirKodSearchResults: Array<{ label: string; value: number }> = [];
    selectedTasinirKodOption: { label: string; value: number } | null = null;
    private tasinirKodSearchSeq = 0;
    readonly malzemeTipleri = MALZEME_TIPLERI;
    paketTurleri: PaketTuruOptionModel[] = [];
    paketTuruSecenekleri: Array<{ label: string; value: string }> = [];
    tesisler: MuhasebeTesisModel[] = [];
    tesisSecenekleri: Array<{ label: string; value: number | null }> = [];
    selectedTesisId: number | null = null;

    ngOnInit(): void {
        this.loadTesisler();
        this.loadPaketTurleri();
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.selectedTesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.applyClientFilter();
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = this.selectedTesisId;
        this.selectedTasinirKodOption = null;
        this.tasinirKodSearchResults = [];
        this.dialogVisible = true;
    }

    openEdit(item: TasinirKartModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.selectedTasinirKodOption = null;
        this.tasinirKodSearchResults = [];

        if (this.model.tasinirKodId > 0) {
            this.tasinirKodService.getById(this.model.tasinirKodId).subscribe({
                next: (kod) => {
                    const option = { label: `${kod.tamKod} - ${kod.ad}`, value: kod.id! };
                    this.selectedTasinirKodOption = option;
                    this.tasinirKodSearchResults = [option];
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
        }

        this.dialogVisible = true;
    }

    searchTasinirKod(event: { query: string }): void {
        const query = event.query ?? '';
        const requestSeq = ++this.tasinirKodSearchSeq;

        if (query.trim().length < 2) {
            this.tasinirKodSearchResults = [];
            this.cdr.detectChanges();
            return;
        }

        this.tasinirKodService.searchPaged(1, 30, query).subscribe({
            next: (paged) => {
                if (requestSeq !== this.tasinirKodSearchSeq) {
                    return;
                }

                this.tasinirKodSearchResults = paged.items
                    .filter((x) => x.aktifMi)
                    .map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                if (requestSeq !== this.tasinirKodSearchSeq) {
                    return;
                }

                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    onTasinirKodSelect(): void {
        this.model.tasinirKodId = this.selectedTasinirKodOption?.value ?? 0;
    }

    onTasinirKodModelChange(value: unknown): void {
        if (typeof value === 'string') {
            this.model.tasinirKodId = 0;
        }
    }

    save(): void {
        this.model.tasinirKodId = this.selectedTasinirKodOption?.value ?? this.model.tasinirKodId;

        if (!this.model.ad?.trim() || !this.model.tasinirKodId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Ad ve tasinir kod secimi zorunludur.' });
            return;
        }

        this.saving = true;
        const payload = {
            tesisId: this.model.tesisId ?? null,
            tasinirKodId: this.model.tasinirKodId,
            stokKodu: null,
            ad: this.model.ad.trim(),
            birim: this.model.birim?.trim() || 'Adet',
            malzemeTipi: this.model.malzemeTipi,
            sarfMi: this.model.sarfMi,
            demirbasMi: this.model.demirbasMi,
            takipliMi: this.model.takipliMi,
            kdvOrani: this.model.kdvOrani,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload)
            : this.service.create(payload);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    delete(item: TasinirKartModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Kayit silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
                        this.cdr.detectChanges();
                    },
                    error: (error: unknown) => {
                        this.showError(error);
                        this.cdr.detectChanges();
                    }
                });
            }
        });
    }

    onTesisFilterChange(): void {
        this.pageNumber = 1;
        this.load(1, this.pageSize);
    }

    getTesisAdi(tesisId?: number | null): string {
        if (!tesisId) {
            return '-';
        }
        return this.tesisler.find((x) => x.id === tesisId)?.ad ?? `#${tesisId}`;
    }

    private createEmpty(): TasinirKartModel {
        return {
            tesisId: null,
            tasinirKodId: 0,
            stokKodu: '',
            muhasebeHesapPlaniId: null,
            anaMuhasebeHesapKodu: null,
            muhasebeHesapSiraNo: null,
            ad: '',
            birim: 'Adet',
            malzemeTipi: 'Diger',
            sarfMi: false,
            demirbasMi: false,
            takipliMi: false,
            kdvOrani: 20,
            aktifMi: true,
            aciklama: null
        };
    }

    private applyClientFilter(): void {
        if (!this.selectedTesisId) {
            this.filteredRecords = [...this.records];
            return;
        }
        this.filteredRecords = this.records.filter((x) => x.tesisId === this.selectedTesisId);
    }

    private loadTesisler(): void {
        this.service.getTesisler().subscribe({
            next: (items) => {
                this.tesisler = [...items].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? ''));
                this.tesisSecenekleri = [{ label: 'Tum Tesisler', value: null }, ...this.tesisler.map((x) => ({ label: x.ad, value: x.id }))];
                if (!this.selectedTesisId && this.tesisler.length > 0) {
                    this.selectedTesisId = this.tesisler[0].id;
                }
                this.load(1, this.pageSize);
            },
            error: (error: unknown) => {
                this.showError(error);
                this.load(1, this.pageSize);
            }
        });
    }

    private loadPaketTurleri(): void {
        this.service.getPaketTurleri().subscribe({
            next: (items) => {
                this.paketTurleri = items.filter((x) => x.aktifMi);
                this.paketTuruSecenekleri = this.paketTurleri
                    .map((x) => ({ label: `${x.ad} (${x.kisaAd})`, value: x.ad }))
                    .sort((a, b) => a.label.localeCompare(b.label));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

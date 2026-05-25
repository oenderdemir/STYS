import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { CariHareketModel, CreateCariHareketRequest, HAREKET_DURUMLARI, UpdateCariHareketRequest } from './cari-hareketler.dto';
import { CariHareketlerService } from './cari-hareketler.service';

@Component({
    selector: 'app-cari-hareketler-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, SelectModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './cari-hareketler.html',
    providers: [MessageService]
})
export class CariHareketlerPage implements OnInit {
    private readonly service = inject(CariHareketlerService);
    private readonly cariKartService = inject(CariKartlarService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    dialogVisible = false;
    records: CariHareketModel[] = [];
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    cariKartlar: Array<{ label: string; value: number; tesisId?: number | null }> = [];
    selectedCariKartId: number | null = null;
    dialogMode: 'create' | 'edit' = 'create';
    model: CariHareketModel = this.createEmpty();
    readonly durumlar = HAREKET_DURUMLARI;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.pageNumber = 1;
            this.closeOpenDialogForTesisChange();
            this.loadCariKartlar();
            this.load(1, this.pageSize);
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için cari hareketler yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadCariKartlar();
                this.load(1, this.pageSize);
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.selectedCariKartId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
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
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        if (this.selectedCariKartId) {
            this.model.cariKartId = this.selectedCariKartId;
        }
        this.dialogVisible = true;
    }

    openEdit(item: CariHareketModel): void {
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (this.getSeciliTesisIdOrWarn() === null) {
            return;
        }

        if (!this.model.cariKartId || !this.model.belgeTuru?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Cari ve belge turu zorunludur.' });
            return;
        }

        const payload: CreateCariHareketRequest | UpdateCariHareketRequest = {
            cariKartId: this.model.cariKartId,
            hareketTarihi: this.model.hareketTarihi,
            belgeTuru: this.model.belgeTuru,
            belgeNo: this.model.belgeNo || null,
            aciklama: this.model.aciklama || null,
            borcTutari: this.model.borcTutari ?? 0,
            alacakTutari: this.model.alacakTutari ?? 0,
            paraBirimi: this.model.paraBirimi || 'TRY',
            vadeTarihi: this.model.vadeTarihi || null,
            durum: this.model.durum,
            kaynakModul: this.model.kaynakModul || null,
            kaynakId: this.model.kaynakId ?? null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateCariHareketRequest)
            : this.service.create(payload as CreateCariHareketRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: CariHareketModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => this.load(),
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadCariKartlar(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.cariKartlar = [];
            this.selectedCariKartId = null;
            return;
        }

        this.cariKartService.getAll().subscribe({
            next: (items) => {
                this.cariKartlar = items
                    .filter((x) => !x.tesisId || x.tesisId === tesisId)
                    .map((x) => ({ label: `${x.cariKodu} - ${x.unvanAdSoyad}`, value: x.id!, tesisId: x.tesisId ?? null }));

                if (this.selectedCariKartId && !this.cariKartlar.some((x) => x.value === this.selectedCariKartId)) {
                    this.selectedCariKartId = null;
                }

                this.cdr.detectChanges();
            }
        });
    }

    private closeOpenDialogForTesisChange(): void {
        if (!this.dialogVisible) {
            return;
        }

        this.dialogVisible = false;
        this.model = this.createEmpty();
    }

    private getSeciliTesisIdOrWarn(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe işlemi için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    private createEmpty(): CariHareketModel {
        return {
            cariKartId: 0,
            hareketTarihi: new Date().toISOString(),
            belgeTuru: '',
            belgeNo: null,
            aciklama: null,
            borcTutari: 0,
            alacakTutari: 0,
            paraBirimi: 'TRY',
            vadeTarihi: null,
            durum: 'Aktif',
            kaynakModul: null,
            kaynakId: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}


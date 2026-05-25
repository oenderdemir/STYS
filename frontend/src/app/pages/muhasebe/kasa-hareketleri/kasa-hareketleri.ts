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
import { KasaBankaHesaplariService } from '../kasa-banka-hesaplari/kasa-banka-hesaplari.service';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { CreateKasaHareketRequest, KasaHareketModel, KASA_HAREKET_TIPLERI, UpdateKasaHareketRequest } from './kasa-hareketleri.dto';
import { KasaHareketleriService } from './kasa-hareketleri.service';

@Component({
    selector: 'app-kasa-hareketleri-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, SelectModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './kasa-hareketleri.html',
    providers: [MessageService]
})
export class KasaHareketleriPage implements OnInit {
    private readonly service = inject(KasaHareketleriService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly kasaBankaHesapService = inject(KasaBankaHesaplariService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    records: KasaHareketModel[] = [];
    model: KasaHareketModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    cariKartlar: Array<{ label: string; value: number; tesisId?: number | null }> = [];
    kasaHesaplar: Array<{ label: string; value: number; tesisId?: number | null }> = [];
    readonly hareketTipleri = KASA_HAREKET_TIPLERI;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.pageNumber = 1;
            this.closeOpenDialogForTesisChange();
            this.loadReferences();
            this.load(1, this.pageSize);
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için kasa hareketleri yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.loadReferences();
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
        this.service.getPaged(pageNumber, pageSize).pipe(finalize(() => {
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
        this.dialogVisible = true;
    }

    openEdit(item: KasaHareketModel): void {
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

        if (!this.model.kasaBankaHesapId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kasa hesabi secimi zorunludur.' });
            return;
        }

        const payload: CreateKasaHareketRequest | UpdateKasaHareketRequest = {
            kasaKodu: this.model.kasaKodu,
            kasaBankaHesapId: this.model.kasaBankaHesapId ?? null,
            hareketTarihi: this.model.hareketTarihi,
            hareketTipi: this.model.hareketTipi,
            tutar: this.model.tutar,
            paraBirimi: this.model.paraBirimi,
            aciklama: this.model.aciklama || null,
            belgeNo: this.model.belgeNo || null,
            cariKartId: this.model.cariKartId ?? null,
            kaynakModul: this.model.kaynakModul || null,
            kaynakId: this.model.kaynakId ?? null,
            durum: this.model.durum
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateKasaHareketRequest)
            : this.service.create(payload as CreateKasaHareketRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadReferences(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.cariKartlar = [];
            this.kasaHesaplar = [];
            return;
        }

        this.cariKartService.getAll().subscribe({
            next: (items) => {
                this.cariKartlar = items
                    .filter((x) => !x.tesisId || x.tesisId === tesisId)
                    .map((x) => ({ label: `${x.cariKodu} - ${x.unvanAdSoyad}`, value: x.id!, tesisId: x.tesisId ?? null }));
                this.cdr.detectChanges();
            }
        });
        this.kasaBankaHesapService.getByTip('NakitKasa', true).subscribe({
            next: (items) => {
                this.kasaHesaplar = items
                    .filter((x) => !x.tesisId || x.tesisId === tesisId)
                    .map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id!, tesisId: x.tesisId ?? null }));
                this.cdr.detectChanges();
            }
        });
    }

    delete(item: KasaHareketModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => this.load(),
            error: (error: unknown) => this.showError(error)
        });
    }

    onKasaHesapChange(): void {
        if (!this.model.kasaBankaHesapId) {
            this.model.kasaKodu = '';
            return;
        }

        const selected = this.kasaHesaplar.find((x) => x.value === this.model.kasaBankaHesapId);
        this.model.kasaKodu = selected?.label?.split(' - ')[0] ?? '';
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

    private createEmpty(): KasaHareketModel {
        return {
            kasaKodu: '',
            kasaBankaHesapId: null,
            hareketTarihi: new Date().toISOString(),
            hareketTipi: 'Tahsilat',
            tutar: 0,
            paraBirimi: 'TRY',
            aciklama: null,
            belgeNo: null,
            cariKartId: null,
            kaynakModul: null,
            kaynakId: null,
            durum: 'Aktif'
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}


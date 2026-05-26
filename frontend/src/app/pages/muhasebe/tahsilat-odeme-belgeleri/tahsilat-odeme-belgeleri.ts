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
import { BELGE_TIPLERI, CreateTahsilatOdemeBelgesiRequest, ODEME_YONTEMLERI, TahsilatOdemeBelgesiModel, TahsilatOdemeOzetModel, UpdateTahsilatOdemeBelgesiRequest } from './tahsilat-odeme-belgeleri.dto';
import { TahsilatOdemeBelgeleriService } from './tahsilat-odeme-belgeleri.service';

@Component({
    selector: 'app-tahsilat-odeme-belgeleri-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, SelectModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './tahsilat-odeme-belgeleri.html',
    providers: [MessageService]
})
export class TahsilatOdemeBelgeleriPage implements OnInit {
    private readonly service = inject(TahsilatOdemeBelgeleriService);
    private readonly cariKartService = inject(CariKartlarService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    records: TahsilatOdemeBelgesiModel[] = [];
    model: TahsilatOdemeBelgesiModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    cariKartlar: Array<{ label: string; value: number; tesisId?: number | null }> = [];
    gunlukOzet: TahsilatOdemeOzetModel | null = null;

    readonly belgeTipleri = BELGE_TIPLERI;
    readonly odemeYontemleri = ODEME_YONTEMLERI;

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
            this.loadOzet();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için tahsilat/ödeme belgeleri yenilendi.'
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
                this.loadOzet();
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
        this.service.getPaged(pageNumber, pageSize, tesisId).pipe(finalize(() => {
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

    loadOzet(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.gunlukOzet = null;
            return;
        }

        this.service.getGunlukOzet(null, tesisId).subscribe({
            next: (ozet) => {
                this.gunlukOzet = ozet;
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

    openEdit(item: TahsilatOdemeBelgesiModel): void {
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

        if (!this.model.belgeNo?.trim() || !this.model.cariKartId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Belge no ve cari secimi zorunludur.' });
            return;
        }

        const payload: CreateTahsilatOdemeBelgesiRequest | UpdateTahsilatOdemeBelgesiRequest = {
            belgeNo: this.model.belgeNo,
            belgeTarihi: this.model.belgeTarihi,
            belgeTipi: this.model.belgeTipi,
            cariKartId: this.model.cariKartId,
            tutar: this.model.tutar,
            paraBirimi: this.model.paraBirimi,
            odemeYontemi: this.model.odemeYontemi,
            aciklama: this.model.aciklama || null,
            kaynakModul: this.model.kaynakModul || null,
            kaynakId: this.model.kaynakId ?? null,
            kapatilacakCariHareketId: this.model.kapatilacakCariHareketId ?? null,
            durum: this.model.durum
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateTahsilatOdemeBelgesiRequest)
            : this.service.create(payload as CreateTahsilatOdemeBelgesiRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.loadOzet();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadCariKartlar(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            this.cariKartlar = [];
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

    delete(item: TahsilatOdemeBelgesiModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => {
                this.load();
                this.loadOzet();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): TahsilatOdemeBelgesiModel {
        return {
            belgeNo: '',
            belgeTarihi: new Date().toISOString(),
            belgeTipi: 'Tahsilat',
            cariKartId: 0,
            tutar: 0,
            paraBirimi: 'TRY',
            odemeYontemi: 'Nakit',
            aciklama: null,
            kaynakModul: null,
            kaynakId: null,
            kapatilacakCariHareketId: null,
            durum: 'Aktif'
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}


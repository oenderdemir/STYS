import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CARI_TIPLERI, CariKartModel, CreateCariKartRequest, UpdateCariKartRequest } from './cari-kartlar.dto';
import { CariKartlarService } from './cari-kartlar.service';

@Component({
    selector: 'app-cari-kartlar-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, SelectModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './cari-kartlar.html',
    providers: [MessageService, ConfirmationService]
})
export class CariKartlarPage implements OnInit {
    private readonly service = inject(CariKartlarService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: CariKartModel[] = [];
    model: CariKartModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    readonly cariTipleri = CARI_TIPLERI;

    ngOnInit(): void {
        this.load(1, this.pageSize);
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
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
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.dialogVisible = true;
    }

    openEdit(item: CariKartModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.cariKodu?.trim() || !this.model.unvanAdSoyad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Cari kodu ve unvan zorunludur.' });
            return;
        }

        const payload: CreateCariKartRequest | UpdateCariKartRequest = {
            cariTipi: this.model.cariTipi,
            cariKodu: this.model.cariKodu.trim(),
            unvanAdSoyad: this.model.unvanAdSoyad.trim(),
            vergiNoTckn: this.model.vergiNoTckn?.trim() || null,
            vergiDairesi: this.model.vergiDairesi?.trim() || null,
            telefon: this.model.telefon?.trim() || null,
            eposta: this.model.eposta?.trim() || null,
            adres: this.model.adres?.trim() || null,
            il: this.model.il?.trim() || null,
            ilce: this.model.ilce?.trim() || null,
            aktifMi: this.model.aktifMi,
            eFaturaMukellefiMi: this.model.eFaturaMukellefiMi,
            eArsivKapsamindaMi: this.model.eArsivKapsamindaMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateCariKartRequest)
            : this.service.create(payload as CreateCariKartRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: CariKartModel): void {
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
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    private createEmpty(): CariKartModel {
        return {
            cariTipi: 'Musteri',
            cariKodu: '',
            unvanAdSoyad: '',
            vergiNoTckn: null,
            vergiDairesi: null,
            telefon: null,
            eposta: null,
            adres: null,
            il: null,
            ilce: null,
            aktifMi: true,
            eFaturaMukellefiMi: false,
            eArsivKapsamindaMi: false,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}


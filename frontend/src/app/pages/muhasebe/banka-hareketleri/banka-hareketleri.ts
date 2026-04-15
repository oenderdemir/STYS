import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { BankaHareketModel, CreateBankaHareketRequest, UpdateBankaHareketRequest } from './banka-hareketleri.dto';
import { BankaHareketleriService } from './banka-hareketleri.service';

@Component({
    selector: 'app-banka-hareketleri-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, InputNumberModule, InputTextModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './banka-hareketleri.html',
    providers: [MessageService]
})
export class BankaHareketleriPage implements OnInit {
    private readonly service = inject(BankaHareketleriService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    records: BankaHareketModel[] = [];
    model: BankaHareketModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

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

    openEdit(item: BankaHareketModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.bankaAdi?.trim() || !this.model.hesapKoduIban?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Banka adi ve hesap/iban zorunludur.' });
            return;
        }

        const payload: CreateBankaHareketRequest | UpdateBankaHareketRequest = {
            bankaAdi: this.model.bankaAdi,
            hesapKoduIban: this.model.hesapKoduIban,
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
            ? this.service.update(this.model.id, payload as UpdateBankaHareketRequest)
            : this.service.create(payload as CreateBankaHareketRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: BankaHareketModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => this.load(),
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): BankaHareketModel {
        return {
            bankaAdi: '',
            hesapKoduIban: '',
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


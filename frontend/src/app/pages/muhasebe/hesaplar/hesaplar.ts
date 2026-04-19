import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CreateHesapRequest, HesapLookupModel, HesapModel, UpdateHesapRequest } from './hesaplar.dto';
import { HesaplarService } from './hesaplar.service';

@Component({
    selector: 'app-hesaplar-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, DialogModule, InputTextModule, MultiSelectModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './hesaplar.html',
    providers: [MessageService]
})
export class HesaplarPage implements OnInit {
    private readonly service = inject(HesaplarService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: HesapModel[] = [];
    model: HesapModel = this.createEmpty();

    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    muhasebeKodlari: Array<{ label: string; value: number }> = [];
    kasaHesaplari: Array<{ label: string; value: number }> = [];
    bankaHesaplari: Array<{ label: string; value: number }> = [];
    depolar: Array<{ label: string; value: number }> = [];

    ngOnInit(): void {
        this.loadLookups();
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
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.dialogVisible = true;
    }

    openEdit(item: HesapModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.service.getById(item.id).subscribe({
            next: (detail) => {
                this.model = {
                    ...detail,
                    kasaHesapIds: detail.kasaHesapIds ?? [],
                    bankaHesapIds: detail.bankaHesapIds ?? [],
                    depoIds: detail.depoIds ?? []
                };
                this.dialogVisible = true;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    save(): void {
        if (!this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Hesap adi zorunludur.' });
            return;
        }

        if (!this.model.muhasebeHesapPlaniId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Muhasebe kodu secimi zorunludur.' });
            return;
        }

        const payload: CreateHesapRequest | UpdateHesapRequest = {
            ad: this.model.ad.trim(),
            muhasebeHesapPlaniId: this.model.muhasebeHesapPlaniId,
            genelHesapMi: this.model.genelHesapMi,
            muhasebeFormu: this.model.muhasebeFormu?.trim() || null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null,
            kasaHesapIds: this.model.kasaHesapIds ?? [],
            bankaHesapIds: this.model.bankaHesapIds ?? [],
            depoIds: this.model.depoIds ?? []
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateHesapRequest)
            : this.service.create(payload as CreateHesapRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Hesap kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: HesapModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => {
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Hesap silindi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private loadLookups(): void {
        this.service.getMuhasebeKodlari('6').subscribe({
            next: (items) => {
                this.muhasebeKodlari = this.mapLookup(items);
                this.cdr.detectChanges();
            }
        });

        this.service.getKasaHesaplari().subscribe({ next: (items) => { this.kasaHesaplari = this.mapLookup(items); this.cdr.detectChanges(); } });
        this.service.getBankaHesaplari().subscribe({ next: (items) => { this.bankaHesaplari = this.mapLookup(items); this.cdr.detectChanges(); } });
        this.service.getDepolar().subscribe({ next: (items) => { this.depolar = this.mapLookup(items); this.cdr.detectChanges(); } });
    }

    private mapLookup(items: HesapLookupModel[]): Array<{ label: string; value: number }> {
        return items.map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id }));
    }

    private createEmpty(): HesapModel {
        return {
            ad: '',
            muhasebeHesapPlaniId: 0,
            genelHesapMi: false,
            muhasebeFormu: null,
            aktifMi: true,
            aciklama: null,
            kasaHesapIds: [],
            bankaHesapIds: [],
            depoIds: []
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

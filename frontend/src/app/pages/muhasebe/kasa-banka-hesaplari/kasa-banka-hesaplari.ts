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
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CreateKasaBankaHesapRequest, KASA_BANKA_HESAP_TIPLERI, KasaBankaHesapModel, KasaBankaHesapTipi, UpdateKasaBankaHesapRequest } from './kasa-banka-hesaplari.dto';
import { KasaBankaHesaplariService } from './kasa-banka-hesaplari.service';

@Component({
    selector: 'app-kasa-banka-hesaplari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, CheckboxModule, DialogModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './kasa-banka-hesaplari.html',
    providers: [MessageService]
})
export class KasaBankaHesaplariPage implements OnInit {
    private readonly service = inject(KasaBankaHesaplariService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    records: KasaBankaHesapModel[] = [];
    model: KasaBankaHesapModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    readonly hesapTipleri = KASA_BANKA_HESAP_TIPLERI;
    muhasebeHesapSecenekleri: Array<{ label: string; value: number }> = [];

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

    openCreate(tip: KasaBankaHesapTipi): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty(tip);
        this.dialogVisible = true;
        this.loadMuhasebeHesapSecimleri(tip);
    }

    openEdit(item: KasaBankaHesapModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
        this.loadMuhasebeHesapSecimleri(this.model.tip);
    }

    onTipChange(): void {
        this.model.muhasebeHesapPlaniId = 0;
        if (this.model.tip === 'NakitKasa') {
            this.model.bankaAdi = null;
            this.model.subeAdi = null;
            this.model.hesapNo = null;
            this.model.iban = null;
            this.model.musteriNo = null;
            this.model.hesapTuru = null;
        }

        this.loadMuhasebeHesapSecimleri(this.model.tip);
    }

    save(): void {
        if (!this.model.kod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kod ve ad zorunludur.' });
            return;
        }

        if (!this.model.muhasebeHesapPlaniId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Muhasebe kodu secimi zorunludur.' });
            return;
        }

        const payload: CreateKasaBankaHesapRequest | UpdateKasaBankaHesapRequest = {
            tip: this.model.tip,
            kod: this.model.kod.trim(),
            ad: this.model.ad.trim(),
            muhasebeHesapPlaniId: this.model.muhasebeHesapPlaniId,
            bankaAdi: this.model.bankaAdi?.trim() || null,
            subeAdi: this.model.subeAdi?.trim() || null,
            hesapNo: this.model.hesapNo?.trim() || null,
            iban: this.model.iban?.trim() || null,
            musteriNo: this.model.musteriNo?.trim() || null,
            hesapTuru: this.model.hesapTuru?.trim() || null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateKasaBankaHesapRequest)
            : this.service.create(payload as CreateKasaBankaHesapRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: KasaBankaHesapModel): void {
        if (!item.id) {
            return;
        }

        this.service.delete(item.id).subscribe({
            next: () => {
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    getTipLabel(tip: string): string {
        return this.hesapTipleri.find((x) => x.value === tip)?.label ?? tip;
    }

    private loadMuhasebeHesapSecimleri(tip: KasaBankaHesapTipi): void {
        this.service.getMuhasebeHesapSecimleri(tip).subscribe({
            next: (items) => {
                this.muhasebeHesapSecenekleri = items.map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id }));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.muhasebeHesapSecenekleri = [];
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    private createEmpty(tip: KasaBankaHesapTipi = 'NakitKasa'): KasaBankaHesapModel {
        return {
            tip,
            kod: '',
            ad: '',
            muhasebeHesapPlaniId: 0,
            bankaAdi: null,
            subeAdi: null,
            hesapNo: null,
            iban: null,
            musteriNo: null,
            hesapTuru: null,
            aktifMi: true,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

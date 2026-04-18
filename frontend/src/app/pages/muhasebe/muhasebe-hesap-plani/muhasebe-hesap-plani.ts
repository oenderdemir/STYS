import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CreateMuhasebeHesapPlaniRequest, MuhasebeHesapPlaniModel, UpdateMuhasebeHesapPlaniRequest } from './muhasebe-hesap-plani.dto';
import { MuhasebeHesapPlaniService } from './muhasebe-hesap-plani.service';

@Component({
    selector: 'app-muhasebe-hesap-plani-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './muhasebe-hesap-plani.html',
    providers: [MessageService, ConfirmationService]
})
export class MuhasebeHesapPlaniPage implements OnInit {
    private readonly service = inject(MuhasebeHesapPlaniService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: MuhasebeHesapPlaniModel[] = [];
    model: MuhasebeHesapPlaniModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    ustHesapSecenekleri: Array<{ label: string; value: number }> = [];

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
        this.prepareUstHesapSecenekleri();
        this.dialogVisible = true;
    }

    openEdit(item: MuhasebeHesapPlaniModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.prepareUstHesapSecenekleri(item.id ?? null);
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.kod?.trim() || !this.model.tamKod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kod, tam kod ve ad zorunludur.' });
            return;
        }

        const payload: CreateMuhasebeHesapPlaniRequest | UpdateMuhasebeHesapPlaniRequest = {
            kod: this.model.kod.trim(),
            tamKod: this.model.tamKod.trim(),
            ad: this.model.ad.trim(),
            seviyeNo: this.model.seviyeNo,
            ustHesapId: this.model.ustHesapId ?? null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateMuhasebeHesapPlaniRequest)
            : this.service.create(payload as CreateMuhasebeHesapPlaniRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: MuhasebeHesapPlaniModel): void {
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

    private prepareUstHesapSecenekleri(excludeId: number | null = null): void {
        this.ustHesapSecenekleri = this.records
            .filter((x) => x.id && x.id !== excludeId)
            .sort((a, b) => a.tamKod.localeCompare(b.tamKod))
            .map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id! }));
    }

    private createEmpty(): MuhasebeHesapPlaniModel {
        return {
            kod: '',
            tamKod: '',
            ad: '',
            seviyeNo: 1,
            ustHesapId: null,
            aktifMi: true,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

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
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { ImportTasinirKodlariRequest, TasinirKodModel } from './tasinir-kodlari.dto';
import { TasinirKodlariService } from './tasinir-kodlari.service';

@Component({
    selector: 'app-tasinir-kodlari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule],
    templateUrl: './tasinir-kodlari.html',
    providers: [MessageService, ConfirmationService]
})
export class TasinirKodlariPage implements OnInit {
    private readonly service = inject(TasinirKodlariService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: TasinirKodModel[] = [];
    model: TasinirKodModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    ngOnInit(): void {
        setTimeout(() => this.load(1, this.pageSize));
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

    openEdit(item: TasinirKodModel): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.tamKod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tam kod ve ad zorunludur.' });
            return;
        }

        this.saving = true;
        const payload = {
            tamKod: this.model.tamKod.trim(),
            duzey1Kod: this.model.duzey1Kod?.trim() || null,
            duzey2Kod: this.model.duzey2Kod?.trim() || null,
            duzey3Kod: this.model.duzey3Kod?.trim() || null,
            duzey4Kod: this.model.duzey4Kod?.trim() || null,
            duzey5Kod: this.model.duzey5Kod?.trim() || null,
            ad: this.model.ad.trim(),
            duzeyNo: this.model.duzeyNo,
            ustKodId: this.model.ustKodId ?? null,
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
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: TasinirKodModel): void {
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

    importOrnekVeri(): void {
        const payload: ImportTasinirKodlariRequest = {
            mevcutlariGuncelle: true,
            pasiflestirilmeyenleriPasifYap: false,
            satirlar: [
                { tamKod: '150.01', duzey1Kod: '150', duzey2Kod: '01', ad: 'Tuketim Malzemeleri', duzeyNo: 2, aktifMi: true },
                { tamKod: '150.01.01', duzey1Kod: '150', duzey2Kod: '01', duzey3Kod: '01', ad: 'Temizlik Malzemeleri', duzeyNo: 3, ustTamKod: '150.01', aktifMi: true },
                { tamKod: '253.01', duzey1Kod: '253', duzey2Kod: '01', ad: 'Makine ve Cihazlar', duzeyNo: 2, aktifMi: true }
            ]
        };

        this.service.import(payload).subscribe({
            next: (sonuc) => {
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Import Tamamlandi', detail: `Eklenen: ${sonuc.eklenen}, Guncellenen: ${sonuc.guncellenen}` });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): TasinirKodModel {
        return {
            tamKod: '',
            ad: '',
            duzeyNo: 1,
            aktifMi: true,
            ustKodId: null,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

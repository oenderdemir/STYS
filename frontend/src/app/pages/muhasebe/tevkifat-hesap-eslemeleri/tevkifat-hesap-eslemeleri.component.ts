import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { finalize } from 'rxjs';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { HesaplarService } from '../hesaplar/hesaplar.service';
import { HesapLookupModel } from '../hesaplar/hesaplar.dto';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import {
    CreateTevkifatHesapEslemeRequest,
    TEVKIFAT_ISLEM_YONLERI,
    TevkifatHesapEslemeFilterDto,
    TevkifatHesapEslemeModel,
    UpdateTevkifatHesapEslemeRequest,
    createDefaultTevkifatHesapEslemeFilter
} from './tevkifat-hesap-eslemeleri.dto';
import { TevkifatHesapEslemeleriService } from './tevkifat-hesap-eslemeleri.service';

@Component({
    selector: 'app-tevkifat-hesap-eslemeleri-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DialogModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TextareaModule,
        ToastModule,
        ToolbarModule,
        MuhasebeTesisContextBarComponent
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './tevkifat-hesap-eslemeleri.component.html',
    styleUrls: ['./tevkifat-hesap-eslemeleri.component.scss']
})
export class TevkifatHesapEslemeleriPage implements OnInit {
    private readonly service = inject(TevkifatHesapEslemeleriService);
    private readonly hesaplarService = inject(HesaplarService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    records: TevkifatHesapEslemeModel[] = [];
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    filter: TevkifatHesapEslemeFilterDto = createDefaultTevkifatHesapEslemeFilter();
    model: TevkifatHesapEslemeModel = this.createEmpty();
    hesapSecenekleri: Array<{ label: string; value: number }> = [];

    readonly islemYonuSecenekleri = TEVKIFAT_ISLEM_YONLERI;
    readonly aktifSecenekleri: Array<{ label: string; value: boolean | null }> = [
        { label: 'Tümü', value: null },
        { label: 'Aktif', value: true },
        { label: 'Pasif', value: false }
    ];

    get tesisSecenekleriForSelect(): Array<{ label: string; value: number | null }> {
        return [
            { label: 'Tümü / Global', value: null },
            ...this.tesisContext.tesisSecenekleri().map((x) => ({ label: x.label, value: x.value }))
        ];
    }

    get islemYonuFiltreSecenekleri(): Array<{ label: string; value: 'Satis' | 'Alis' | null }> {
        return [
            { label: 'Tümü', value: null },
            ...this.islemYonuSecenekleri.map((x) => ({ label: x.label, value: x.value }))
        ];
    }

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({ error: () => void 0 });
        this.loadLookups();
        this.load(1, this.pageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, this.filter).pipe(finalize(() => (this.loading = false))).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onLazyLoad(event: { first?: number | null; rows?: number | null }): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    applyFilter(): void {
        this.pageNumber = 1;
        this.load(1, this.pageSize);
    }

    clearFilter(): void {
        this.filter = createDefaultTevkifatHesapEslemeFilter();
        this.load(1, this.pageSize);
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        this.dialogVisible = true;
    }

    openEdit(item: TevkifatHesapEslemeModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.islemYonu) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Islem yonu zorunludur.' });
            return;
        }

        if (!this.model.tevkifatPay || !this.model.tevkifatPayda || this.model.tevkifatPay <= 0 || this.model.tevkifatPayda <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tevkifat orani zorunludur.' });
            return;
        }

        if (!this.model.muhasebeHesapPlaniId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Muhasebe hesabi zorunludur.' });
            return;
        }

        const payload: CreateTevkifatHesapEslemeRequest | UpdateTevkifatHesapEslemeRequest = {
            tesisId: this.model.tesisId ?? null,
            islemYonu: this.model.islemYonu,
            tevkifatPay: Number(this.model.tevkifatPay),
            tevkifatPayda: Number(this.model.tevkifatPayda),
            muhasebeHesapPlaniId: Number(this.model.muhasebeHesapPlaniId),
            aktifMi: !!this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateTevkifatHesapEslemeRequest)
            : this.service.create(payload as CreateTevkifatHesapEslemeRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: TevkifatHesapEslemeModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Tevkifat hesap esleme silinsin mi?',
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

    getTesisAdi(tesisId?: number | null): string {
        if (!tesisId) {
            return 'Global';
        }

        const tesis = this.tesisContext.tesisler().find((x) => x.id === tesisId);
        return tesis?.ad ?? `#${tesisId}`;
    }

    getHesapLabel(id?: number | null): string {
        if (!id) {
            return '-';
        }

        const hesap = this.hesapSecenekleri.find((x) => x.value === id);
        return hesap?.label ?? `#${id}`;
    }

    private loadLookups(): void {
        this.hesaplarService.getMuhasebeKodlari('').subscribe({
            next: (items) => {
                this.hesapSecenekleri = items.map((x: HesapLookupModel) => ({ label: `${x.kod} - ${x.ad}`, value: x.id }));
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): TevkifatHesapEslemeModel {
        return {
            tesisId: null,
            islemYonu: 'Satis',
            tevkifatPay: 7,
            tevkifatPayda: 10,
            muhasebeHesapPlaniId: 0,
            aktifMi: true,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const detail = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail });
    }
}

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
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import {
    CreateKonaklamaVergisiHesapEslemeRequest,
    KonaklamaVergisiHesapEslemeFilterDto,
    KonaklamaVergisiHesapEslemeModel,
    KonaklamaVergisiHesapSecenekModel,
    UpdateKonaklamaVergisiHesapEslemeRequest,
    createDefaultKonaklamaVergisiHesapEslemeFilter
} from './konaklama-vergisi-hesap-eslemeleri.dto';
import { KonaklamaVergisiHesapEslemeleriService } from './konaklama-vergisi-hesap-eslemeleri.service';

@Component({
    selector: 'app-konaklama-vergisi-hesap-eslemeleri-page',
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
    templateUrl: './konaklama-vergisi-hesap-eslemeleri.component.html',
    styleUrls: ['./konaklama-vergisi-hesap-eslemeleri.component.scss']
})
export class KonaklamaVergisiHesapEslemeleriPage implements OnInit {
    private readonly service = inject(KonaklamaVergisiHesapEslemeleriService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    records: KonaklamaVergisiHesapEslemeModel[] = [];
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;
    filter: KonaklamaVergisiHesapEslemeFilterDto = createDefaultKonaklamaVergisiHesapEslemeFilter();
    model: KonaklamaVergisiHesapEslemeModel = this.createEmpty();
    hesapSecenekleri: Array<{ label: string; value: number }> = [];

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

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({ error: () => void 0 });
        this.loadHesapSecenekleri();
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
        this.load(Math.floor(nextFirst / nextPageSize) + 1, nextPageSize);
    }

    applyFilter(): void {
        this.pageNumber = 1;
        this.loadHesapSecenekleri();
        this.load(1, this.pageSize);
    }

    clearFilter(): void {
        this.filter = createDefaultKonaklamaVergisiHesapEslemeFilter();
        this.loadHesapSecenekleri();
        this.load(1, this.pageSize);
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        this.loadHesapSecenekleri(this.model.tesisId);
        this.dialogVisible = true;
    }

    openEdit(item: KonaklamaVergisiHesapEslemeModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.loadHesapSecenekleri(this.model.tesisId);
        this.dialogVisible = true;
    }

    onModelTesisChanged(): void {
        this.model.vergiHesapId = 0;
        this.loadHesapSecenekleri(this.model.tesisId);
    }

    save(): void {
        if (!this.model.vergiHesapId) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Konaklama vergisi hesabı zorunludur.' });
            return;
        }

        const payload: CreateKonaklamaVergisiHesapEslemeRequest | UpdateKonaklamaVergisiHesapEslemeRequest = {
            tesisId: this.model.tesisId ?? null,
            vergiHesapId: Number(this.model.vergiHesapId),
            aktifMi: !!this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateKonaklamaVergisiHesapEslemeRequest)
            : this.service.create(payload as CreateKonaklamaVergisiHesapEslemeRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Kayıt kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: KonaklamaVergisiHesapEslemeModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Konaklama vergisi hesap eşlemesi silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayır',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Kayıt silindi.' });
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

    private loadHesapSecenekleri(tesisId: number | null | undefined = this.filter.tesisId): void {
        this.service.getHesapSecenekleri(tesisId ?? null).subscribe({
            next: (items: KonaklamaVergisiHesapSecenekModel[]) => {
                this.hesapSecenekleri = items.map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id }));
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): KonaklamaVergisiHesapEslemeModel {
        return {
            tesisId: null,
            vergiHesapId: 0,
            aktifMi: true,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const detail = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail });
    }
}


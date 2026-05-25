import { CommonModule, DatePipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import {
    CreateMuhasebeDonemRequest,
    MuhasebeDonemDto,
    UpdateMuhasebeDonemRequest,
    createDefaultDonemFilter
} from '../models/muhasebe-donem.model';
import { MuhasebeDonemService } from '../services/muhasebe-donem.service';

const MALI_YIL_SECENEKLERI: Array<{ label: string; value: number | null }> = (() => {
    const currentYear = new Date().getFullYear();
    const options: Array<{ label: string; value: number | null }> = [
        { label: 'Tümü', value: null }
    ];
    for (let y = currentYear - 3; y <= currentYear + 3; y++) {
        options.push({ label: String(y), value: y });
    }
    return options;
})();

const DURUM_SECENEKLERI: Array<{ label: string; value: boolean | null }> = [
    { label: 'Tümü', value: null },
    { label: 'Açık', value: false },
    { label: 'Kapalı', value: true }
];

@Component({
    selector: 'app-muhasebe-donemler',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        DatePipe,
        ButtonModule,
        ConfirmDialogModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        TooltipModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './muhasebe-donemler.component.html',
    styleUrl: './muhasebe-donemler.component.scss',
    providers: [MessageService, ConfirmationService]
})
export class MuhasebeDonemlerComponent implements OnInit {
    private readonly service = inject(MuhasebeDonemService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    allRecords: MuhasebeDonemDto[] = [];
    filteredRecords: MuhasebeDonemDto[] = [];

    filter = createDefaultDonemFilter();
    model: MuhasebeDonemDto = this.createEmpty();

    readonly maliYilSecenekleri = MALI_YIL_SECENEKLERI;
    readonly durumSecenekleri = DURUM_SECENEKLERI;

    // Row-level action loading states
    kapatilanDonemId: number | null = null;
    acilanDonemId: number | null = null;
    silinenDonemId: number | null = null;
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.filter.tesisId = tesisId;
            if (this.dialogVisible) {
                this.dialogVisible = false;
                this.messageService.add({
                    severity: UiSeverity.Warn,
                    summary: 'Çalışma Tesisi Değişti',
                    detail: 'Çalışma tesisi değiştiği için açık dönem formu kapatıldı.'
                });
            }
            this.loadDonemler();
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                if (this.currentTesisId) {
                    this.filter.tesisId = this.currentTesisId;
                }
                this.loadDonemler();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    loadDonemler(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        this.loading = true;
        this.service.getAll(tesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (records) => {
                this.allRecords = records;
                this.applyFilter();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    applyFilter(): void {
        let result = [...this.allRecords];

        if (this.filter.tesisId != null) {
            result = result.filter(r => r.tesisId === this.filter.tesisId);
        }

        if (this.filter.maliYil != null) {
            result = result.filter(r => r.maliYil === this.filter.maliYil);
        }

        if (this.filter.kapaliMi != null) {
            result = result.filter(r => r.kapaliMi === this.filter.kapaliMi);
        }

        // Sort: by MaliYil desc, then DonemNo desc
        result.sort((a, b) => {
            if (a.maliYil !== b.maliYil) return b.maliYil - a.maliYil;
            return b.donemNo - a.donemNo;
        });

        this.filteredRecords = result;
    }

    onFilterChange(): void {
        this.applyFilter();
    }

    openCreate(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = tesisId;
        this.dialogVisible = true;
    }

    openEdit(item: MuhasebeDonemDto): void {
        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
    }

    save(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }
        if (this.dialogMode === 'create') {
            this.model.tesisId = tesisId;
        }
        if (!this.model.maliYil || this.model.maliYil < 2000 || this.model.maliYil > 2100) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Geçerli bir mali yıl girilmelidir (2000-2100).' });
            return;
        }
        if (!this.model.donemNo || this.model.donemNo < 1 || this.model.donemNo > 12) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Dönem no 1-12 aralığında olmalıdır.' });
            return;
        }
        if (!this.model.baslangicTarihi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Başlangıç tarihi zorunludur.' });
            return;
        }
        if (!this.model.bitisTarihi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Bitiş tarihi zorunludur.' });
            return;
        }
        if (new Date(this.model.baslangicTarihi) >= new Date(this.model.bitisTarihi)) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Geçersiz Tarih', detail: 'Başlangıç tarihi bitiş tarihinden önce olmalıdır.' });
            return;
        }

        this.saving = true;

        if (this.dialogMode === 'create') {
            const request: CreateMuhasebeDonemRequest = {
                tesisId: this.model.tesisId,
                maliYil: this.model.maliYil,
                donemNo: this.model.donemNo,
                baslangicTarihi: this.model.baslangicTarihi,
                bitisTarihi: this.model.bitisTarihi,
                aciklama: this.model.aciklama || null
            };
            this.service.create(request).pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            })).subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Dönem oluşturuldu.' });
                    this.dialogVisible = false;
                    this.loadDonemler();
                },
                error: (error: unknown) => {
                    this.showError(error);
                }
            });
        } else {
            const request: UpdateMuhasebeDonemRequest = {
                tesisId: this.model.tesisId,
                maliYil: this.model.maliYil,
                donemNo: this.model.donemNo,
                baslangicTarihi: this.model.baslangicTarihi,
                bitisTarihi: this.model.bitisTarihi,
                kapaliMi: this.model.kapaliMi,
                aciklama: this.model.aciklama || null
            };
            this.service.update(this.model.id, request).pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            })).subscribe({
                next: () => {
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Dönem güncellendi.' });
                    this.dialogVisible = false;
                    this.loadDonemler();
                },
                error: (error: unknown) => {
                    this.showError(error);
                }
            });
        }
    }

    confirmKapat(item: MuhasebeDonemDto): void {
        this.confirmationService.confirm({
            key: 'donemDialog',
            header: 'Dönem Kapat',
            message: `${item.tesisAdi || item.tesisId} - ${item.maliYil} / Dönem ${item.donemNo} kapatılacaktır.\n\nKapatılan döneme fiş girişi yapılamaz. Onaylı fişler etkilenmez.`,
            icon: 'pi pi-lock',
            acceptLabel: 'Kapat',
            rejectLabel: 'Vazgeç',
            acceptButtonStyleClass: 'p-button-warning',
            accept: () => {
                this.kapatDonem(item);
            }
        });
    }

    private kapatDonem(item: MuhasebeDonemDto): void {
        this.kapatilanDonemId = item.id;
        this.cdr.detectChanges();

        this.service.kapat(item.id).pipe(finalize(() => {
            this.kapatilanDonemId = null;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Dönem kapatıldı.' });
                this.loadDonemler();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    confirmAc(item: MuhasebeDonemDto): void {
        this.confirmationService.confirm({
            key: 'donemDialog',
            header: 'Dönem Aç',
            message: `${item.tesisAdi || item.tesisId} - ${item.maliYil} / Dönem ${item.donemNo} yeniden açılacaktır.\n\nAçılan döneme tekrar fiş girişi yapılabilir.`,
            icon: 'pi pi-unlock',
            acceptLabel: 'Aç',
            rejectLabel: 'Vazgeç',
            acceptButtonStyleClass: 'p-button-success',
            accept: () => {
                this.acDonem(item);
            }
        });
    }

    private acDonem(item: MuhasebeDonemDto): void {
        this.acilanDonemId = item.id;
        this.cdr.detectChanges();

        this.service.ac(item.id).pipe(finalize(() => {
            this.acilanDonemId = null;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Dönem açıldı.' });
                this.loadDonemler();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    confirmDelete(item: MuhasebeDonemDto): void {
        if (item.kapaliMi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Silinemez', detail: 'Kapalı dönem silinemez.' });
            return;
        }

        this.confirmationService.confirm({
            key: 'donemDialog',
            header: 'Dönem Sil',
            message: `${item.tesisAdi || item.tesisId} - ${item.maliYil} / Dönem ${item.donemNo} silinecektir.\n\nBu işlem geri alınamaz.`,
            icon: 'pi pi-trash',
            acceptLabel: 'Sil',
            rejectLabel: 'Vazgeç',
            acceptButtonStyleClass: 'p-button-danger',
            accept: () => {
                this.deleteDonem(item);
            }
        });
    }

    private deleteDonem(item: MuhasebeDonemDto): void {
        this.silinenDonemId = item.id;
        this.cdr.detectChanges();

        this.service.delete(item.id).pipe(finalize(() => {
            this.silinenDonemId = null;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Başarılı', detail: 'Dönem silindi.' });
                this.loadDonemler();
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    isEditingDisabled(item: MuhasebeDonemDto): boolean {
        // Kapalı dönemde sadece Açıklama düzenlenebilir; backend'de de böyle
        return false; // Dialog içinde gerekirse ek validasyon olabilir
    }

    getDurumSeverity(kapaliMi: boolean): 'success' | 'danger' {
        return kapaliMi ? 'danger' : 'success';
    }

    getDurumLabel(kapaliMi: boolean): string {
        return kapaliMi ? 'Kapalı' : 'Açık';
    }

    private createEmpty(): MuhasebeDonemDto {
        const today = new Date();
        return {
            id: 0,
            tesisId: this.filter.tesisId ?? 0,
            tesisAdi: null,
            maliYil: today.getFullYear(),
            donemNo: today.getMonth() + 1,
            baslangicTarihi: '',
            bitisTarihi: '',
            kapaliMi: false,
            kapanisTarihi: null,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const msg = tryReadApiMessage(error);
        this.messageService.add({
            severity: UiSeverity.Error,
            summary: 'Hata',
            detail: msg ?? 'Bir hata oluştu.',
            life: 8000
        });
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
}

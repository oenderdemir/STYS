import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { parseApiDate, toLocalDateString } from '../../../core/utils/date-time.util';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { finalize } from 'rxjs';
import { tryReadApiMessage } from '../../../core/api';
import {
    CreateKdvIstisnaTanimRequest,
    ISTISNA_SECENEKLERI,
    KDV_UYGULAMA_TIPI_LABELS,
    KdvIstisnaTanimDto,
    KdvIstisnaTanimFilterDto,
    KdvUygulamaTipi,
    UpdateKdvIstisnaTanimRequest,
    createDefaultKdvIstisnaTanimFilter
} from '../models/kdv-istisna-tanim.model';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';

@Component({
    selector: 'app-kdv-istisna-tanimlari',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        InputTextModule,
        MuhasebeTesisContextBarComponent,
        TextareaModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToggleSwitchModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './kdv-istisna-tanimlari.component.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    styleUrls: ['./kdv-istisna-tanimlari.component.scss']
})
export class KdvIstisnaTanimlariComponent implements OnInit {
    private readonly service = inject(KdvIstisnaTanimService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly fb = inject(FormBuilder);

    items: KdvIstisnaTanimDto[] = [];
    loading = false;
    saving = false;

    dialogVisible = false;
    isEditing = false;
    editForm!: FormGroup;
    editingItem: KdvIstisnaTanimDto | null = null;

    filter!: KdvIstisnaTanimFilterDto;
    filterForm!: FormGroup;

    uygulamaTipiSecenekleri = ISTISNA_SECENEKLERI;

    readonly AKTIF_SECENEKLERI: Array<{ label: string; value: boolean | null }> = [
        { label: 'Tümü', value: null },
        { label: 'Aktif', value: true },
        { label: 'Pasif', value: false }
    ];

    readonly KULLANIM_SECENEKLERI: Array<{ label: string; value: boolean | null }> = [
        { label: 'Tümü', value: null },
        { label: 'Evet', value: true },
        { label: 'Hayır', value: false }
    ];

    readonly FILTER_UYGULAMA_TIPI_SECENEKLERI: Array<{ label: string; value: KdvUygulamaTipi | null }> = [{ label: 'Tümü', value: null }, ...ISTISNA_SECENEKLERI];

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({ error: () => void 0 });
        this.filter = createDefaultKdvIstisnaTanimFilter();
        this.buildFilterForm();
        this.buildForm();
        this.loadItems();
    }

    private buildFilterForm(): void {
        this.filterForm = this.fb.group({
            kod: [null],
            ad: [null],
            uygulamaTipi: [null],
            aktifMi: [null],
            satisIslemlerindeKullanilirMi: [null],
            alisIslemlerindeKullanilirMi: [null]
        });
    }

    private buildForm(): void {
        this.editForm = this.fb.group({
            kod: ['', [Validators.required, Validators.maxLength(50)]],
            ad: ['', [Validators.required, Validators.maxLength(250)]],
            aciklama: [null],
            uygulamaTipi: [2, Validators.required],
            satisIslemlerindeKullanilirMi: [false],
            alisIslemlerindeKullanilirMi: [false],
            yuklenilenKdvIndirilebilirMi: [false],
            iadeHakkiVarMi: [false],
            eBelgeKoduZorunluMu: [false],
            aktifMi: [true],
            gecerlilikBaslangicTarihi: [null],
            gecerlilikBitisTarihi: [null]
        });
    }

    applyFilter(): void {
        const formValue = this.filterForm.value;
        this.filter = {
            kod: formValue.kod?.trim() || null,
            ad: formValue.ad?.trim() || null,
            uygulamaTipi: formValue.uygulamaTipi ?? null,
            aktifMi: formValue.aktifMi ?? null,
            satisIslemlerindeKullanilirMi: formValue.satisIslemlerindeKullanilirMi ?? null,
            alisIslemlerindeKullanilirMi: formValue.alisIslemlerindeKullanilirMi ?? null
        };
        this.loadItems();
    }

    clearFilter(): void {
        this.filterForm.reset({
            kod: null,
            ad: null,
            uygulamaTipi: null,
            aktifMi: null,
            satisIslemlerindeKullanilirMi: null,
            alisIslemlerindeKullanilirMi: null
        });
        this.filter = createDefaultKdvIstisnaTanimFilter();
        this.loadItems();
    }

    private loadItems(): void {
        this.loading = true;
        this.service
            .filter(this.filter)
            .pipe(finalize(() => (this.loading = false)))
            .subscribe({
                next: (records) => (this.items = records),
                error: (error) => this.showError(error)
            });
    }

    getUygulamaTipiLabel(tip: KdvUygulamaTipi): string {
        return KDV_UYGULAMA_TIPI_LABELS[tip] ?? String(tip);
    }

    getUygulamaTipiSeverity(tip: KdvUygulamaTipi): 'info' | 'warn' | 'success' | 'secondary' {
        switch (tip) {
            case 2:
                return 'success';
            case 3:
                return 'warn';
            case 4:
                return 'secondary';
            case 5:
                return 'info';
            default:
                return 'secondary';
        }
    }

    openCreate(): void {
        this.isEditing = false;
        this.editingItem = null;
        this.editForm.reset({
            kod: '',
            ad: '',
            aciklama: null,
            uygulamaTipi: 2,
            satisIslemlerindeKullanilirMi: false,
            alisIslemlerindeKullanilirMi: false,
            yuklenilenKdvIndirilebilirMi: false,
            iadeHakkiVarMi: false,
            eBelgeKoduZorunluMu: false,
            aktifMi: true,
            gecerlilikBaslangicTarihi: null,
            gecerlilikBitisTarihi: null
        });
        this.dialogVisible = true;
    }

    openEdit(item: KdvIstisnaTanimDto): void {
        this.isEditing = true;
        this.editingItem = item;
        this.editForm.reset({
            kod: item.kod,
            ad: item.ad,
            aciklama: item.aciklama,
            uygulamaTipi: item.uygulamaTipi,
            satisIslemlerindeKullanilirMi: item.satisIslemlerindeKullanilirMi,
            alisIslemlerindeKullanilirMi: item.alisIslemlerindeKullanilirMi,
            yuklenilenKdvIndirilebilirMi: item.yuklenilenKdvIndirilebilirMi,
            iadeHakkiVarMi: item.iadeHakkiVarMi,
            eBelgeKoduZorunluMu: item.eBelgeKoduZorunluMu,
            aktifMi: item.aktifMi,
            gecerlilikBaslangicTarihi: parseApiDate(item.gecerlilikBaslangicTarihi),
            gecerlilikBitisTarihi: parseApiDate(item.gecerlilikBitisTarihi)
        });
        this.dialogVisible = true;
    }

    save(): void {
        if (this.editForm.invalid) return;

        const formValue = this.editForm.value;

        const baslangicDate: Date | null = formValue.gecerlilikBaslangicTarihi instanceof Date ? formValue.gecerlilikBaslangicTarihi : null;
        const bitisDate: Date | null = formValue.gecerlilikBitisTarihi instanceof Date ? formValue.gecerlilikBitisTarihi : null;

        if (baslangicDate && bitisDate && bitisDate.getTime() <= baslangicDate.getTime()) {
            this.messageService.add({
                severity: 'error',
                summary: 'Hata',
                detail: 'Geçerlilik bitiş tarihi, başlangıç tarihinden sonra olmalıdır.'
            });
            return;
        }

        this.saving = true;

        const baslangic = toLocalDateString(baslangicDate);
        const bitis = toLocalDateString(bitisDate);

        if (this.isEditing && this.editingItem) {
            const request: UpdateKdvIstisnaTanimRequest = {
                kod: formValue.kod,
                ad: formValue.ad,
                aciklama: formValue.aciklama ?? null,
                uygulamaTipi: formValue.uygulamaTipi,
                satisIslemlerindeKullanilirMi: formValue.satisIslemlerindeKullanilirMi,
                alisIslemlerindeKullanilirMi: formValue.alisIslemlerindeKullanilirMi,
                yuklenilenKdvIndirilebilirMi: formValue.yuklenilenKdvIndirilebilirMi,
                iadeHakkiVarMi: formValue.iadeHakkiVarMi,
                eBelgeKoduZorunluMu: formValue.eBelgeKoduZorunluMu,
                aktifMi: formValue.aktifMi,
                gecerlilikBaslangicTarihi: baslangic,
                gecerlilikBitisTarihi: bitis
            };

            this.service
                .update(this.editingItem.id, request)
                .pipe(finalize(() => (this.saving = false)))
                .subscribe({
                    next: () => {
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Başarılı',
                            detail: 'KDV istisna tanımı güncellendi.'
                        });
                        this.dialogVisible = false;
                        this.loadItems();
                    },
                    error: (error) => this.showError(error)
                });
        } else {
            const request: CreateKdvIstisnaTanimRequest = {
                kod: formValue.kod,
                ad: formValue.ad,
                aciklama: formValue.aciklama ?? null,
                uygulamaTipi: formValue.uygulamaTipi,
                satisIslemlerindeKullanilirMi: formValue.satisIslemlerindeKullanilirMi,
                alisIslemlerindeKullanilirMi: formValue.alisIslemlerindeKullanilirMi,
                yuklenilenKdvIndirilebilirMi: formValue.yuklenilenKdvIndirilebilirMi,
                iadeHakkiVarMi: formValue.iadeHakkiVarMi,
                eBelgeKoduZorunluMu: formValue.eBelgeKoduZorunluMu,
                aktifMi: formValue.aktifMi,
                gecerlilikBaslangicTarihi: baslangic,
                gecerlilikBitisTarihi: bitis
            };

            this.service
                .create(request)
                .pipe(finalize(() => (this.saving = false)))
                .subscribe({
                    next: () => {
                        this.messageService.add({
                            severity: 'success',
                            summary: 'Başarılı',
                            detail: 'KDV istisna tanımı oluşturuldu.'
                        });
                        this.dialogVisible = false;
                        this.loadItems();
                    },
                    error: (error) => this.showError(error)
                });
        }
    }

    confirmDelete(item: KdvIstisnaTanimDto): void {
        this.confirmationService.confirm({
            message: `"${item.kod} - ${item.ad}" tanımını silmek istediğinize emin misiniz?`,
            header: 'Silme Onayı',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Sil',
            rejectLabel: 'İptal',
            acceptButtonStyleClass: 'p-button-danger',
            accept: () => this.deleteItem(item)
        });
    }

    private deleteItem(item: KdvIstisnaTanimDto): void {
        this.service.delete(item.id).subscribe({
            next: () => {
                this.messageService.add({
                    severity: 'success',
                    summary: 'Başarılı',
                    detail: 'KDV istisna tanımı silindi.'
                });
                this.loadItems();
            },
            error: (error) => this.showError(error)
        });
    }

    private showError(error: unknown): void {
        const detail = tryReadApiMessage(error) ?? 'Bir hata oluştu.';
        this.messageService.add({
            severity: 'error',
            summary: 'Hata',
            detail
        });
    }
}

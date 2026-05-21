import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import {
    FormBuilder,
    FormGroup,
    ReactiveFormsModule,
    Validators
} from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { finalize } from 'rxjs';
import {
    CreateKdvIstisnaTanimRequest,
    KDV_UYGULAMA_TIPI_LABELS,
    KDV_UYGULAMA_TIPI_SECENEKLERI,
    KdvIstisnaTanimDto,
    KdvUygulamaTipi,
    UpdateKdvIstisnaTanimRequest
} from '../models/kdv-istisna-tanim.model';
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
        DialogModule,
        InputTextModule,
        TextareaModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToggleSwitchModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './kdv-istisna-tanimlari.component.html',
    styleUrls: ['./kdv-istisna-tanimlari.component.scss']
})
export class KdvIstisnaTanimlariComponent implements OnInit {
    private readonly service = inject(KdvIstisnaTanimService);
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

    uygulamaTipiSecenekleri = KDV_UYGULAMA_TIPI_SECENEKLERI;

    ngOnInit(): void {
        this.buildForm();
        this.loadItems();
    }

    private buildForm(): void {
        this.editForm = this.fb.group({
            kod: ['', [Validators.required, Validators.maxLength(50)]],
            ad: ['', [Validators.required, Validators.maxLength(250)]],
            aciklama: [null],
            uygulamaTipi: [1, Validators.required],
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

    private loadItems(): void {
        this.loading = true;
        this.service
            .getAll()
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
            case 1:
                return 'info';
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
            uygulamaTipi: 1,
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
            gecerlilikBaslangicTarihi: item.gecerlilikBaslangicTarihi
                ? this.toDateInputValue(item.gecerlilikBaslangicTarihi)
                : null,
            gecerlilikBitisTarihi: item.gecerlilikBitisTarihi
                ? this.toDateInputValue(item.gecerlilikBitisTarihi)
                : null
        });
        this.dialogVisible = true;
    }

    private toDateInputValue(isoString: string): string {
        if (!isoString) return '';
        try {
            const d = new Date(isoString);
            if (isNaN(d.getTime())) return isoString.substring(0, 10);
            return d.toISOString().substring(0, 10);
        } catch {
            return isoString.substring(0, 10);
        }
    }

    save(): void {
        if (this.editForm.invalid) return;

        this.saving = true;
        const formValue = this.editForm.value;

        const baslangic = formValue.gecerlilikBaslangicTarihi
            ? new Date(formValue.gecerlilikBaslangicTarihi).toISOString()
            : null;
        const bitis = formValue.gecerlilikBitisTarihi
            ? new Date(formValue.gecerlilikBitisTarihi).toISOString()
            : null;

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
        const detail = error instanceof Error ? error.message : 'Bir hata oluştu.';
        this.messageService.add({
            severity: 'error',
            summary: 'Hata',
            detail
        });
    }
}

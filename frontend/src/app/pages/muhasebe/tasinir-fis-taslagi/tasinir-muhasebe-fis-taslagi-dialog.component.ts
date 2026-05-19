import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ToastModule } from 'primeng/toast';
import { HttpErrorResponse } from '@angular/common/http';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import {
    TasinirMuhasebeFisiOlusturRequestModel,
    TasinirMuhasebeFisiOlusturResultModel,
    createDefaultTasinirFisRequest
} from '../models/tasinir-muhasebe-fis.model';
import { TasinirMuhasebeFisService } from '../services/tasinir-muhasebe-fis.service';

interface TesisSecenek {
    label: string;
    value: number;
}

const DONEM_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Seçiniz (opsiyonel)', value: null },
    { label: '1. Dönem', value: 1 },
    { label: '2. Dönem', value: 2 },
    { label: '3. Dönem', value: 3 },
    { label: '4. Dönem', value: 4 },
    { label: '5. Dönem', value: 5 },
    { label: '6. Dönem', value: 6 },
    { label: '7. Dönem', value: 7 },
    { label: '8. Dönem', value: 8 },
    { label: '9. Dönem', value: 9 },
    { label: '10. Dönem', value: 10 },
    { label: '11. Dönem', value: 11 },
    { label: '12. Dönem', value: 12 }
];

const KDV_ORAN_SECENEKLERI: Array<{ label: string; value: number | null }> = [
    { label: 'Seçiniz', value: null },
    { label: '%1', value: 1 },
    { label: '%8', value: 8 },
    { label: '%10', value: 10 },
    { label: '%18', value: 18 },
    { label: '%20', value: 20 }
];

@Component({
    selector: 'app-tasinir-muhasebe-fis-taslagi-dialog',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        DecimalPipe,
        ButtonModule,
        CheckboxModule,
        DatePickerModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        ToastModule
    ],
    templateUrl: './tasinir-muhasebe-fis-taslagi-dialog.component.html',
    styleUrls: ['./tasinir-muhasebe-fis-taslagi-dialog.component.scss'],
    providers: [MessageService]
})
export class TasinirMuhasebeFisTaslagiDialogComponent implements OnInit {
    private readonly service = inject(TasinirMuhasebeFisService);
    private readonly messageService = inject(MessageService);
    private readonly ref = inject(DynamicDialogRef);
    private readonly config = inject(DynamicDialogConfig);

    loading = false;
    tesislerLoading = false;
    kdvEnabled = false;
    result: TasinirMuhasebeFisiOlusturResultModel | null = null;

    request: TasinirMuhasebeFisiOlusturRequestModel = createDefaultTasinirFisRequest();
    tesisSecenekleri: TesisSecenek[] = [];
    readonly donemSecenekleri = DONEM_SECENEKLERI;
    readonly kdvOranSecenekleri = KDV_ORAN_SECENEKLERI;

    ngOnInit(): void {
        this.loadTesisler();
    }

    private loadTesisler(): void {
        this.tesislerLoading = true;
        this.service.getTesisler().pipe(
            finalize(() => {
                this.tesislerLoading = false;
            })
        ).subscribe({
            next: (tesisler) => {
                this.tesisSecenekleri = tesisler
                    .sort((a, b) => a.ad.localeCompare(b.ad))
                    .map((t) => ({ label: t.ad, value: t.id }));
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    private validate(): boolean {
        if (!this.request.tesisId) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Tesis seçimi zorunludur.'
            });
            return false;
        }

        if (!this.request.maliYil) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Mali yıl zorunludur.'
            });
            return false;
        }

        if (!this.request.fisTarihi) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Fiş tarihi zorunludur.'
            });
            return false;
        }

        if (!this.request.tasinirKodu?.trim()) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Taşınır kodu zorunludur.'
            });
            return false;
        }

        if (!this.request.tutar || this.request.tutar <= 0) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: 'Tutar sıfırdan büyük olmalıdır.'
            });
            return false;
        }

        if (this.kdvEnabled) {
            if (!this.request.kdvOrani || this.request.kdvOrani <= 0 || this.request.kdvOrani > 100) {
                this.messageService.add({
                    severity: UiSeverity.Warn,
                    summary: 'Eksik Bilgi',
                    detail: 'KDV oranı 0 ile 100 arasında olmalıdır.'
                });
                return false;
            }

            if (!this.request.kdvHesapKodu?.trim()) {
                this.messageService.add({
                    severity: UiSeverity.Warn,
                    summary: 'Eksik Bilgi',
                    detail: 'KDV hesap kodu zorunludur.'
                });
                return false;
            }
        }

        return true;
    }

    olustur(): void {
        if (!this.validate()) {
            return;
        }

        this.loading = true;
        this.result = null;

        const payload: TasinirMuhasebeFisiOlusturRequestModel = {
            ...this.request,
            tesisId: this.request.tesisId!,
            maliYil: this.request.maliYil!,
            tutar: this.request.tutar!,
            kdvDahilMi: this.kdvEnabled ? this.request.kdvDahilMi : false,
            kdvOrani: this.kdvEnabled ? this.request.kdvOrani : null,
            kdvHesapKodu: this.kdvEnabled ? this.request.kdvHesapKodu : null
        };

        this.service.createTasinirFisTaslagi(payload).pipe(
            finalize(() => {
                this.loading = false;
            })
        ).subscribe({
            next: (result) => {
                this.result = result;
                this.messageService.add({
                    severity: UiSeverity.Success,
                    summary: 'Başarılı',
                    detail: `Taşınır muhasebe fişi (${result.fisNo}) taslak olarak oluşturuldu.`
                });
            },
            error: (error: unknown) => {
                this.showError(error);
            }
        });
    }

    kapat(): void {
        this.ref.close();
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

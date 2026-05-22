import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
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
    private readonly router = inject(Router);

    loading = false;
    tesislerLoading = false;
    kdvEnabled = false;
    result: TasinirMuhasebeFisiOlusturResultModel | null = null;

    request: TasinirMuhasebeFisiOlusturRequestModel = createDefaultTasinirFisRequest();
    tesisSecenekleri: TesisSecenek[] = [];
    readonly donemSecenekleri = DONEM_SECENEKLERI;
    readonly kdvOranSecenekleri = KDV_ORAN_SECENEKLERI;

    ngOnInit(): void {
        const data = this.config.data as Partial<TasinirMuhasebeFisiOlusturRequestModel> | undefined;
        if (data) {
            if (data.tesisId != null) { this.request.tesisId = data.tesisId; }
            if (data.tasinirKodu != null) { this.request.tasinirKodu = data.tasinirKodu; }
            if (data.tutar != null) { this.request.tutar = data.tutar; }
            if (data.referansTipi != null) { this.request.referansTipi = data.referansTipi; }
            if (data.referansId != null) { this.request.referansId = data.referansId; }
            if (data.aciklama != null) { this.request.aciklama = data.aciklama; }
            if (data.belgeNo != null) { this.request.belgeNo = data.belgeNo; }
            if (data.fisTarihi != null) { this.request.fisTarihi = data.fisTarihi; }
            if (data.maliYil != null) { this.request.maliYil = data.maliYil; }
            if (data.donem != null) { this.request.donem = data.donem; }
            if (data.alacakHesapKodu != null) { this.request.alacakHesapKodu = data.alacakHesapKodu; }
            if (data.kdvUygulamaTipi != null) { this.request.kdvUygulamaTipi = data.kdvUygulamaTipi; }
            if (data.kdvIstisnaKodu != null) { this.request.kdvIstisnaKodu = data.kdvIstisnaKodu; }
            if (data.kdvIstisnaAciklamasi != null) { this.request.kdvIstisnaAciklamasi = data.kdvIstisnaAciklamasi; }
            if (data.hareketTipi != null) { this.request.hareketTipi = data.hareketTipi; }
            if (data.kdvTutari != null) { this.request.kdvTutari = data.kdvTutari; }
        }
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

    private formatDate(value: string | Date): string {
        if (value instanceof Date) {
            const yyyy = value.getFullYear();
            const mm = String(value.getMonth() + 1).padStart(2, '0');
            const dd = String(value.getDate()).padStart(2, '0');
            return `${yyyy}-${mm}-${dd}`;
        }
        return value;
    }

    private trimToNull(value: string | null | undefined): string | null {
        const trimmed = value?.trim();
        return trimmed ? trimmed : null;
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

        if (this.request.maliYil < 2000 || this.request.maliYil > 2100) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Geçersiz Değer',
                detail: 'Mali yıl 2000-2100 aralığında olmalıdır.'
            });
            return false;
        }

        if (this.request.donem && (this.request.donem < 1 || this.request.donem > 12)) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Geçersiz Değer',
                detail: 'Dönem 1-12 aralığında olmalıdır.'
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
            tesisId: this.request.tesisId!,
            maliYil: this.request.maliYil!,
            donem: this.request.donem,
            fisTarihi: this.formatDate(this.request.fisTarihi),
            tasinirKodu: this.request.tasinirKodu.trim(),
            tutar: this.request.tutar!,
            alacakHesapKodu: this.trimToNull(this.request.alacakHesapKodu),
            aciklama: this.trimToNull(this.request.aciklama),
            belgeNo: this.trimToNull(this.request.belgeNo),
            referansTipi: this.trimToNull(this.request.referansTipi),
            referansId: this.trimToNull(this.request.referansId),
            kdvDahilMi: this.kdvEnabled ? this.request.kdvDahilMi : false,
            kdvOrani: this.kdvEnabled ? this.request.kdvOrani : null,
            kdvHesapKodu: this.kdvEnabled ? this.trimToNull(this.request.kdvHesapKodu) : null,
            kdvUygulamaTipi: this.request.kdvUygulamaTipi,
            kdvIstisnaKodu: this.request.kdvIstisnaKodu,
            kdvIstisnaAciklamasi: this.request.kdvIstisnaAciklamasi,
            hareketTipi: this.request.hareketTipi,
            kdvTutari: this.request.kdvTutari
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

    muhasebeFisineGit(): void {
        if (!this.result?.muhasebeFisId) {
            return;
        }
        this.ref.close();
        this.router.navigate(['/muhasebe/fisler'], {
            queryParams: {
                id: this.result.muhasebeFisId,
                fisNo: this.result.fisNo
            }
        });
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'İşlem başarısız.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}

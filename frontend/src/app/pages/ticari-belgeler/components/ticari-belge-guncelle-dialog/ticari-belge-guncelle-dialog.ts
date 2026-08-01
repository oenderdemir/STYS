import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { toLocalDateString } from '../../../../core/utils/date-time.util';
import {
    KDV_UYGULAMA_TIPI_LABELS,
    KdvUygulamaTipi,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    SATIS_BELGESI_TIPI_SECENEKLERI,
    SatisBelgesiSatirTipi,
    TicariBelgeGuncelleRequest,
    TicariBelgeGuncelleSatirRequest,
    createEmptyTicariBelgeGuncelleSatiri
} from '../../ticari-belge.models';

@Component({
    selector: 'app-ticari-belge-guncelle-dialog',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TextareaModule,
        ToggleSwitchModule
    ],
    templateUrl: './ticari-belge-guncelle-dialog.html'
})
export class TicariBelgeGuncelleDialogComponent {
    @Input() visible = false;
    @Input() saving = false;
    @Input() formData: TicariBelgeGuncelleRequest | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() save = new EventEmitter<void>();

    readonly belgeTipiSecenekleri = SATIS_BELGESI_TIPI_SECENEKLERI;
    readonly satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    readonly satirTipiSecenekleri = Object.entries(SATIS_BELGESI_SATIR_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as SatisBelgesiSatirTipi,
        label
    }));
    readonly kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;
    readonly kdvUygulamaTipiSecenekleri = Object.entries(KDV_UYGULAMA_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as KdvUygulamaTipi,
        label
    }));

    belgeTarihiValue(): Date | null {
        return this.formData?.belgeTarihi ? new Date(this.formData.belgeTarihi) : null;
    }

    onBelgeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.belgeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    vadeTarihiValue(): Date | null {
        return this.formData?.vadeTarihi ? new Date(this.formData.vadeTarihi) : null;
    }

    onVadeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.vadeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    addSatir(): void {
        if (!this.formData) return;
        const satirlar = this.formData.satirlar ?? [];
        const yeniSatir = createEmptyTicariBelgeGuncelleSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        this.formData.satirlar = [...satirlar, yeniSatir];
    }

    removeSatir(index: number): void {
        if (!this.formData?.satirlar) return;
        this.formData.satirlar = this.formData.satirlar
            .filter((_, i) => i !== index)
            .map((satir, i) => ({ ...satir, siraNo: i + 1 }));
    }

    trackBySatirIndex(index: number): number {
        return index;
    }

    isTevkifatli(satir: TicariBelgeGuncelleSatirRequest): boolean {
        return satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli;
    }

    onHide(): void {
        this.visibleChange.emit(false);
    }

    onSaveClick(): void {
        this.save.emit();
    }
}

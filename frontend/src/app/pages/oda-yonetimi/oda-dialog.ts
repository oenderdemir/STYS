import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { BinaDto } from '../bina-yonetimi/bina-yonetimi.dto';
import { OdaTipiDto } from '../oda-tipi-yonetimi/oda-tipi-yonetimi.dto';
import { OdaDto } from './oda-yonetimi.dto';

@Component({
    selector: 'app-oda-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, InputNumberModule, SelectModule, ToggleSwitchModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '40rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '95vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12 md:col-span-6">
                    <label for="odaNo" class="block font-medium mb-2">Oda No</label>
                    <input id="odaNo" pInputText [(ngModel)]="workingModel.odaNo" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="katNo" class="block font-medium mb-2">Kat No</label>
                    <p-inputnumber inputId="katNo" [(ngModel)]="workingModel.katNo" [useGrouping]="false" styleClass="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="binaId" class="block font-medium mb-2">Bina</label>
                    <p-select
                        inputId="binaId"
                        [options]="binalar"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.binaId"
                        (ngModelChange)="onBinaChange()"
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="tesisOdaTipiId" class="block font-medium mb-2">Oda Tipi</label>
                    <p-select
                        inputId="tesisOdaTipiId"
                        [options]="availableOdaTipleri"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.tesisOdaTipiId"
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="yatakSayisi" class="block font-medium mb-2">Yatak Sayisi</label>
                    <p-inputnumber inputId="yatakSayisi" [(ngModel)]="workingModel.yatakSayisi" [min]="0" [useGrouping]="false" styleClass="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6 flex items-center gap-3 mt-7">
                    <p-toggleswitch inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi">Aktif</label>
                </div>
            </div>

            <ng-template #footer>
                <p-button label="Kapat" icon="pi pi-times" severity="secondary" text [disabled]="saving" (onClick)="close()" />
                @if (showSaveButton) {
                    <p-button [label]="saving ? 'Kaydediliyor...' : saveButtonLabel" icon="pi pi-check" [disabled]="saving || !canSubmit()" (onClick)="submit()" />
                }
            </ng-template>
        </p-dialog>
    `
})
export class OdaDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: OdaDto = { odaNo: '', binaId: 0, tesisOdaTipiId: 0, katNo: 0, yatakSayisi: null, aktifMi: true };
    @Input() binalar: BinaDto[] = [];
    @Input() odaTipleri: OdaTipiDto[] = [];
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<OdaDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: OdaDto = { odaNo: '', binaId: 0, tesisOdaTipiId: 0, katNo: 0, yatakSayisi: null, aktifMi: true };

    get availableOdaTipleri(): OdaTipiDto[] {
        const tesisId = this.getSelectedTesisId();
        if (!tesisId) {
            return [];
        }

        return this.odaTipleri.filter((item) => item.tesisId === tesisId);
    }

    get isReadOnly(): boolean {
        return this.mode === 'view' || !this.canManage;
    }

    get showSaveButton(): boolean {
        return this.mode !== 'view' && this.canManage;
    }

    get showLockToggle(): boolean {
        return this.canManage && this.mode !== 'create';
    }

    get lockIcon(): string {
        return this.mode === 'view' ? 'pi pi-lock' : 'pi pi-lock-open';
    }

    get lockSeverity(): 'danger' | 'success' {
        return this.mode === 'view' ? 'danger' : 'success';
    }

    get lockAriaLabel(): string {
        return this.mode === 'view' ? 'Kilitli' : 'Kilit acik';
    }

    get saveButtonLabel(): string {
        return this.mode === 'edit' ? 'Guncelle' : 'Olustur';
    }

    get dialogTitle(): string {
        if (this.mode === 'create') {
            return 'Yeni Oda';
        }

        if (this.mode === 'edit') {
            return 'Oda Duzenle';
        }

        return 'Oda Detay';
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['model']) {
            this.workingModel = { ...this.model };
        }

        if (changes['visible'] && this.visible) {
            this.workingModel = { ...this.model };
        }
    }

    canSubmit(): boolean {
        return (this.workingModel.odaNo?.trim() ?? '').length > 0
            && !!this.workingModel.binaId
            && !!this.workingModel.tesisOdaTipiId;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            odaNo: this.workingModel.odaNo.trim(),
            binaId: this.workingModel.binaId,
            tesisOdaTipiId: this.workingModel.tesisOdaTipiId,
            katNo: this.workingModel.katNo,
            yatakSayisi: this.workingModel.yatakSayisi ?? null,
            aktifMi: this.workingModel.aktifMi
        });
    }

    onBinaChange(): void {
        const tesisId = this.getSelectedTesisId();
        if (!tesisId) {
            this.workingModel.tesisOdaTipiId = 0;
            return;
        }

        const existsInTesis = this.odaTipleri.some((item) => item.id === this.workingModel.tesisOdaTipiId && item.tesisId === tesisId);
        if (!existsInTesis) {
            this.workingModel.tesisOdaTipiId = 0;
        }
    }

    toggleLockMode(): void {
        if (!this.canManage || this.mode === 'create') {
            return;
        }

        if (this.mode === 'view') {
            this.modeChange.emit('edit');
            return;
        }

        this.workingModel = { ...this.model };
        this.modeChange.emit('view');
    }

    close(): void {
        this.visibleChange.emit(false);
    }

    private getSelectedTesisId(): number {
        const selectedBina = this.binalar.find((item) => item.id === this.workingModel.binaId);
        return selectedBina?.tesisId ?? 0;
    }
}

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
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="odaTipiId" class="block font-medium mb-2">Oda Tipi</label>
                    <p-select
                        inputId="odaTipiId"
                        [options]="odaTipleri"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.odaTipiId"
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
    @Input() model: OdaDto = { odaNo: '', binaId: 0, odaTipiId: 0, katNo: 0, yatakSayisi: null, aktifMi: true };
    @Input() binalar: BinaDto[] = [];
    @Input() odaTipleri: OdaTipiDto[] = [];
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<OdaDto>();

    workingModel: OdaDto = { odaNo: '', binaId: 0, odaTipiId: 0, katNo: 0, yatakSayisi: null, aktifMi: true };

    get isReadOnly(): boolean {
        return this.mode === 'view' || !this.canManage;
    }

    get showSaveButton(): boolean {
        return this.mode !== 'view' && this.canManage;
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
            && !!this.workingModel.odaTipiId;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            odaNo: this.workingModel.odaNo.trim(),
            binaId: this.workingModel.binaId,
            odaTipiId: this.workingModel.odaTipiId,
            katNo: this.workingModel.katNo,
            yatakSayisi: this.workingModel.yatakSayisi ?? null,
            aktifMi: this.workingModel.aktifMi
        });
    }

    close(): void {
        this.visibleChange.emit(false);
    }
}

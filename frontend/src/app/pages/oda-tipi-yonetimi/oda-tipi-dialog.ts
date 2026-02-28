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
import { TesisDto } from '../tesis-yonetimi/tesis-yonetimi.dto';
import { OdaSinifiDto, OdaTipiDto } from './oda-tipi-yonetimi.dto';

@Component({
    selector: 'app-oda-tipi-dialog',
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
                    <label for="tesisId" class="block font-medium mb-2">Tesis</label>
                    <p-select
                        inputId="tesisId"
                        [options]="tesisler"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.tesisId"
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="odaSinifiId" class="block font-medium mb-2">Oda Sinifi</label>
                    <p-select
                        inputId="odaSinifiId"
                        [options]="odaSiniflari"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.odaSinifiId"
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="kapasite" class="block font-medium mb-2">Kapasite</label>
                    <p-inputnumber inputId="kapasite" [(ngModel)]="workingModel.kapasite" [min]="1" [useGrouping]="false" styleClass="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="metrekare" class="block font-medium mb-2">Metrekare</label>
                    <p-inputnumber inputId="metrekare" [(ngModel)]="workingModel.metrekare" [min]="0" mode="decimal" [minFractionDigits]="0" [maxFractionDigits]="2" styleClass="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6 flex items-center gap-3 mt-7">
                    <p-toggleswitch inputId="paylasimliMi" [(ngModel)]="workingModel.paylasimliMi" [disabled]="isReadOnly || saving" />
                    <label for="paylasimliMi">Paylasimli</label>
                </div>
                <div class="col-span-12 md:col-span-4 flex items-center gap-3">
                    <p-toggleswitch inputId="balkonVarMi" [(ngModel)]="workingModel.balkonVarMi" [disabled]="isReadOnly || saving" />
                    <label for="balkonVarMi">Balkon</label>
                </div>
                <div class="col-span-12 md:col-span-4 flex items-center gap-3">
                    <p-toggleswitch inputId="klimaVarMi" [(ngModel)]="workingModel.klimaVarMi" [disabled]="isReadOnly || saving" />
                    <label for="klimaVarMi">Klima</label>
                </div>
                <div class="col-span-12 md:col-span-4 flex items-center gap-3">
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
export class OdaTipiDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: OdaTipiDto = { tesisId: 0, odaSinifiId: 0, ad: '', paylasimliMi: false, kapasite: 1, balkonVarMi: false, klimaVarMi: false, metrekare: null, aktifMi: true };
    @Input() tesisler: TesisDto[] = [];
    @Input() odaSiniflari: OdaSinifiDto[] = [];
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<OdaTipiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: OdaTipiDto = { tesisId: 0, odaSinifiId: 0, ad: '', paylasimliMi: false, kapasite: 1, balkonVarMi: false, klimaVarMi: false, metrekare: null, aktifMi: true };

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
            return 'Yeni Oda Tipi';
        }

        if (this.mode === 'edit') {
            return 'Oda Tipi Duzenle';
        }

        return 'Oda Tipi Detay';
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
        return !!this.workingModel.tesisId
            && !!this.workingModel.odaSinifiId
            && (this.workingModel.ad?.trim() ?? '').length > 0
            && (this.workingModel.kapasite ?? 0) > 0;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

            this.save.emit({
                id: this.workingModel.id ?? null,
                tesisId: this.workingModel.tesisId,
                odaSinifiId: this.workingModel.odaSinifiId,
                ad: this.workingModel.ad.trim(),
                paylasimliMi: this.workingModel.paylasimliMi,
                kapasite: this.workingModel.kapasite,
            balkonVarMi: this.workingModel.balkonVarMi,
            klimaVarMi: this.workingModel.klimaVarMi,
            metrekare: this.workingModel.metrekare ?? null,
            aktifMi: this.workingModel.aktifMi
        });
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
}

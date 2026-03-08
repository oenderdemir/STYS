import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { SezonKuraliDto } from './sezon-yonetimi.dto';

interface SelectOption<T = string | number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-sezon-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, CheckboxModule, SelectModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '40rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '92vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12">
                    <label for="tesisId" class="block font-medium mb-2">Tesis</label>
                    <p-select inputId="tesisId" class="w-full" [options]="tesisOptions" optionLabel="label" optionValue="value" [(ngModel)]="workingModel.tesisId" [appendTo]="'body'" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="kod" class="block font-medium mb-2">Kod</label>
                    <input id="kod" pInputText [(ngModel)]="workingModel.kod" class="w-full" [disabled]="isReadOnly || saving" maxlength="64" />
                </div>
                <div class="col-span-12 md:col-span-8">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" maxlength="200" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="baslangicTarihi" class="block font-medium mb-2">Baslangic</label>
                    <input id="baslangicTarihi" pInputText type="date" class="w-full" [(ngModel)]="workingModel.baslangicTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="bitisTarihi" class="block font-medium mb-2">Bitis</label>
                    <input id="bitisTarihi" pInputText type="date" class="w-full" [(ngModel)]="workingModel.bitisTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="minimumGece" class="block font-medium mb-2">Minimum Gece</label>
                    <input id="minimumGece" pInputText type="number" min="1" class="w-full" [(ngModel)]="workingModel.minimumGece" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4 flex items-center">
                    <p-checkbox inputId="stopSaleMi" [(ngModel)]="workingModel.stopSaleMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="stopSaleMi" class="ml-2">Stop Sale</label>
                </div>
                <div class="col-span-12 md:col-span-4 flex items-center">
                    <p-checkbox inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi" class="ml-2">Aktif</label>
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
export class SezonDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: SezonKuraliDto = this.emptyModel();
    @Input() saving = false;
    @Input() canManage = false;
    @Input() tesisOptions: SelectOption<number>[] = [];

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<SezonKuraliDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: SezonKuraliDto = this.emptyModel();

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
            return 'Yeni Sezon Kurali';
        }

        if (this.mode === 'edit') {
            return 'Sezon Kurali Duzenle';
        }

        return 'Sezon Kurali Detay';
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['model']) {
            this.workingModel = this.cloneModel(this.model);
        }

        if (changes['visible'] && this.visible) {
            this.workingModel = this.cloneModel(this.model);
        }
    }

    canSubmit(): boolean {
        const kod = this.workingModel.kod?.trim() ?? '';
        const ad = this.workingModel.ad?.trim() ?? '';
        const hasTesis = (this.workingModel.tesisId ?? 0) > 0;
        const hasDates = !!this.workingModel.baslangicTarihi && !!this.workingModel.bitisTarihi;
        const minGeceValid = Number(this.workingModel.minimumGece) >= 1;

        if (!kod || !ad || !hasTesis || !hasDates || !minGeceValid) {
            return false;
        }

        return new Date(this.workingModel.baslangicTarihi).getTime() <= new Date(this.workingModel.bitisTarihi).getTime();
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            tesisId: this.workingModel.tesisId,
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
            baslangicTarihi: this.workingModel.baslangicTarihi,
            bitisTarihi: this.workingModel.bitisTarihi,
            minimumGece: Number(this.workingModel.minimumGece),
            stopSaleMi: this.workingModel.stopSaleMi,
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

        this.workingModel = this.cloneModel(this.model);
        this.modeChange.emit('view');
    }

    close(): void {
        this.visibleChange.emit(false);
    }

    private cloneModel(source: SezonKuraliDto): SezonKuraliDto {
        return {
            id: source.id ?? null,
            tesisId: source.tesisId,
            kod: source.kod ?? '',
            ad: source.ad ?? '',
            baslangicTarihi: source.baslangicTarihi ?? '',
            bitisTarihi: source.bitisTarihi ?? '',
            minimumGece: source.minimumGece ?? 1,
            stopSaleMi: !!source.stopSaleMi,
            aktifMi: source.aktifMi ?? true
        };
    }

    private emptyModel(): SezonKuraliDto {
        return {
            tesisId: 0,
            kod: '',
            ad: '',
            baslangicTarihi: '',
            bitisTarihi: '',
            minimumGece: 1,
            stopSaleMi: false,
            aktifMi: true
        };
    }
}

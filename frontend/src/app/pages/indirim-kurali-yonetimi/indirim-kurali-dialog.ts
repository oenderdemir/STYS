import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { IndirimKuraliDto } from './indirim-kurali-yonetimi.dto';

export interface SelectOption<T = string | number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-indirim-kurali-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, SelectModule, MultiSelectModule, CheckboxModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '56rem', 'max-width': '96vw' }"
            [breakpoints]="{ '960px': '92vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12 md:col-span-4">
                    <label for="kod" class="block font-medium mb-2">Kod</label>
                    <input id="kod" pInputText [(ngModel)]="workingModel.kod" class="w-full" [disabled]="isReadOnly || saving" maxlength="64" />
                </div>
                <div class="col-span-12 md:col-span-8">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" maxlength="200" />
                </div>

                <div class="col-span-12 md:col-span-4">
                    <label for="indirimTipi" class="block font-medium mb-2">Indirim Tipi</label>
                    <p-select inputId="indirimTipi" class="w-full" [options]="indirimTipiOptions" optionLabel="label" optionValue="value" [(ngModel)]="workingModel.indirimTipi" [appendTo]="'body'" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="deger" class="block font-medium mb-2">Deger</label>
                    <input id="deger" pInputText type="number" min="0.01" step="0.01" class="w-full" [(ngModel)]="workingModel.deger" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="oncelik" class="block font-medium mb-2">Oncelik</label>
                    <input id="oncelik" pInputText type="number" class="w-full" [(ngModel)]="workingModel.oncelik" [disabled]="isReadOnly || saving" />
                </div>

                <div class="col-span-12 md:col-span-4">
                    <label for="kapsamTipi" class="block font-medium mb-2">Kapsam</label>
                    <p-select inputId="kapsamTipi" class="w-full" [options]="kapsamTipiOptions" optionLabel="label" optionValue="value" [(ngModel)]="workingModel.kapsamTipi" [appendTo]="'body'" [disabled]="isReadOnly || saving || !canCreateSystemRule" (ngModelChange)="onKapsamTipiChanged()" />
                </div>
                <div class="col-span-12 md:col-span-8">
                    <label for="tesisId" class="block font-medium mb-2">Tesis</label>
                    <p-select
                        inputId="tesisId"
                        class="w-full"
                        [options]="tesisOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="workingModel.tesisId"
                        [appendTo]="'body'"
                        [disabled]="isReadOnly || saving || workingModel.kapsamTipi !== 'Tesis'"
                        [showClear]="workingModel.kapsamTipi === 'Tesis'"
                    />
                </div>

                <div class="col-span-12 md:col-span-6">
                    <label for="baslangicTarihi" class="block font-medium mb-2">Baslangic</label>
                    <input id="baslangicTarihi" pInputText type="date" class="w-full" [(ngModel)]="workingModel.baslangicTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="bitisTarihi" class="block font-medium mb-2">Bitis</label>
                    <input id="bitisTarihi" pInputText type="date" class="w-full" [(ngModel)]="workingModel.bitisTarihi" [disabled]="isReadOnly || saving" />
                </div>

                <div class="col-span-12 md:col-span-6">
                    <label for="misafirTipiIds" class="block font-medium mb-2">Misafir Tipleri (Opsiyonel)</label>
                    <p-multiSelect
                        inputId="misafirTipiIds"
                        class="w-full"
                        [options]="misafirTipiOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="workingModel.misafirTipiIds"
                        [appendTo]="'body'"
                        [disabled]="isReadOnly || saving"
                        [filter]="true"
                        [showClear]="true"
                        display="chip"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="konaklamaTipiIds" class="block font-medium mb-2">Konaklama Tipleri (Opsiyonel)</label>
                    <p-multiSelect
                        inputId="konaklamaTipiIds"
                        class="w-full"
                        [options]="konaklamaTipiOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="workingModel.konaklamaTipiIds"
                        [appendTo]="'body'"
                        [disabled]="isReadOnly || saving"
                        [filter]="true"
                        [showClear]="true"
                        display="chip"
                    />
                </div>

                <div class="col-span-12 md:col-span-6">
                    <p-checkbox inputId="birlesebilirMi" [(ngModel)]="workingModel.birlesebilirMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="birlesebilirMi" class="ml-2">Birlesebilir</label>
                </div>
                <div class="col-span-12 md:col-span-6">
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
export class IndirimKuraliDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: IndirimKuraliDto = this.emptyModel();
    @Input() saving = false;
    @Input() canManage = false;
    @Input() canCreateSystemRule = false;
    @Input() tesisOptions: SelectOption<number>[] = [];
    @Input() misafirTipiOptions: SelectOption<number>[] = [];
    @Input() konaklamaTipiOptions: SelectOption<number>[] = [];

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<IndirimKuraliDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    readonly indirimTipiOptions: SelectOption<string>[] = [
        { label: 'Yuzde', value: 'Yuzde' },
        { label: 'Tutar', value: 'Tutar' }
    ];

    get kapsamTipiOptions(): SelectOption<string>[] {
        const options: SelectOption<string>[] = [{ label: 'Tesis', value: 'Tesis' }];
        if (this.canCreateSystemRule || this.workingModel.kapsamTipi === 'Sistem') {
            options.unshift({ label: 'Sistem', value: 'Sistem' });
        }

        return options;
    }

    workingModel: IndirimKuraliDto = this.emptyModel();

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
            return 'Yeni Indirim Kurali';
        }

        if (this.mode === 'edit') {
            return 'Indirim Kurali Duzenle';
        }

        return 'Indirim Kurali Detay';
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['model']) {
            this.workingModel = this.cloneModel(this.model);
        }

        if (changes['visible'] && this.visible) {
            this.workingModel = this.cloneModel(this.model);
        }

        if (!this.canCreateSystemRule && this.mode === 'create' && this.workingModel.kapsamTipi === 'Sistem') {
            this.workingModel.kapsamTipi = 'Tesis';
        }
    }

    onKapsamTipiChanged(): void {
        if (this.workingModel.kapsamTipi === 'Sistem') {
            this.workingModel.tesisId = null;
        }
    }

    canSubmit(): boolean {
        const kod = this.workingModel.kod?.trim() ?? '';
        const ad = this.workingModel.ad?.trim() ?? '';

        if (kod.length === 0 || ad.length === 0) {
            return false;
        }

        if (!this.workingModel.indirimTipi || !this.workingModel.kapsamTipi) {
            return false;
        }

        if (!this.workingModel.deger || this.workingModel.deger <= 0) {
            return false;
        }

        if (!this.workingModel.baslangicTarihi || !this.workingModel.bitisTarihi) {
            return false;
        }

        if (this.workingModel.baslangicTarihi > this.workingModel.bitisTarihi) {
            return false;
        }

        if (this.workingModel.kapsamTipi === 'Tesis' && !this.workingModel.tesisId) {
            return false;
        }

        return true;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        const payload = this.cloneModel(this.workingModel);
        payload.kod = payload.kod.trim().toUpperCase();
        payload.ad = payload.ad.trim();
        payload.indirimTipi = payload.indirimTipi.trim();
        payload.kapsamTipi = payload.kapsamTipi.trim();
        payload.tesisId = payload.kapsamTipi === 'Tesis' ? payload.tesisId : null;
        payload.baslangicTarihi = `${payload.baslangicTarihi}T00:00:00`;
        payload.bitisTarihi = `${payload.bitisTarihi}T00:00:00`;
        payload.misafirTipiIds = [...new Set(payload.misafirTipiIds)];
        payload.konaklamaTipiIds = [...new Set(payload.konaklamaTipiIds)];

        this.save.emit(payload);
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

    private cloneModel(source: IndirimKuraliDto): IndirimKuraliDto {
        return {
            id: source.id ?? null,
            kod: source.kod ?? '',
            ad: source.ad ?? '',
            indirimTipi: source.indirimTipi ?? 'Yuzde',
            deger: source.deger ?? 0,
            kapsamTipi: source.kapsamTipi ?? 'Tesis',
            tesisId: source.tesisId ?? null,
            baslangicTarihi: this.normalizeDateInput(source.baslangicTarihi),
            bitisTarihi: this.normalizeDateInput(source.bitisTarihi),
            oncelik: source.oncelik ?? 0,
            birlesebilirMi: source.birlesebilirMi ?? true,
            aktifMi: source.aktifMi ?? true,
            misafirTipiIds: [...(source.misafirTipiIds ?? [])],
            konaklamaTipiIds: [...(source.konaklamaTipiIds ?? [])]
        };
    }

    private normalizeDateInput(value: string): string {
        if (!value) {
            return this.todayInput();
        }

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return this.todayInput();
        }

        return parsed.toISOString().slice(0, 10);
    }

    private todayInput(): string {
        return new Date().toISOString().slice(0, 10);
    }

    private emptyModel(): IndirimKuraliDto {
        const today = this.todayInput();
        return {
            kod: '',
            ad: '',
            indirimTipi: 'Yuzde',
            deger: 0,
            kapsamTipi: 'Tesis',
            tesisId: null,
            baslangicTarihi: today,
            bitisTarihi: today,
            oncelik: 0,
            birlesebilirMi: true,
            aktifMi: true,
            misafirTipiIds: [],
            konaklamaTipiIds: []
        };
    }
}

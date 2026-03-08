import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { OdaKullanimBlokDto, OdaKullanimBlokOdaSecenekDto } from './oda-kullanim-blok-yonetimi.dto';

interface SelectOption<T = string | number> {
    label: string;
    value: T;
}

@Component({
    selector: 'app-oda-kullanim-blok-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, CheckboxModule, SelectModule, TextareaModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '44rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '92vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12 md:col-span-6">
                    <label for="blokTipi" class="block font-medium mb-2">Blok Tipi</label>
                    <p-select
                        inputId="blokTipi"
                        class="w-full"
                        [options]="blokTipiOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="workingModel.blokTipi"
                        [appendTo]="'body'"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6 flex items-center pt-6">
                    <p-checkbox inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi" class="ml-2">Aktif</label>
                </div>
                <div class="col-span-12">
                    <label for="odaId" class="block font-medium mb-2">Oda</label>
                    <p-select
                        inputId="odaId"
                        class="w-full"
                        [options]="odaOptions"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="workingModel.odaId"
                        [appendTo]="'body'"
                        [filter]="true"
                        [showClear]="false"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="baslangicTarihi" class="block font-medium mb-2">Baslangic Tarihi</label>
                    <input
                        id="baslangicTarihi"
                        type="datetime-local"
                        class="w-full p-inputtext p-component"
                        [(ngModel)]="baslangicTarihiInput"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="bitisTarihi" class="block font-medium mb-2">Bitis Tarihi</label>
                    <input
                        id="bitisTarihi"
                        type="datetime-local"
                        class="w-full p-inputtext p-component"
                        [(ngModel)]="bitisTarihiInput"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12">
                    <label for="aciklama" class="block font-medium mb-2">Aciklama</label>
                    <textarea
                        id="aciklama"
                        pTextarea
                        rows="4"
                        class="w-full"
                        [(ngModel)]="workingModel.aciklama"
                        maxlength="512"
                        [disabled]="isReadOnly || saving"
                    ></textarea>
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
export class OdaKullanimBlokDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: OdaKullanimBlokDto = this.emptyModel();
    @Input() saving = false;
    @Input() canManage = false;
    @Input() selectedTesisId: number | null = null;
    @Input() odaSecenekleri: OdaKullanimBlokOdaSecenekDto[] = [];

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<OdaKullanimBlokDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: OdaKullanimBlokDto = this.emptyModel();
    baslangicTarihiInput = '';
    bitisTarihiInput = '';

    readonly blokTipiOptions: SelectOption<string>[] = [
        { label: 'Bakim', value: 'Bakim' },
        { label: 'Ariza', value: 'Ariza' }
    ];

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
            return 'Yeni Oda Bakim/Ariza Kaydi';
        }

        if (this.mode === 'edit') {
            return 'Oda Bakim/Ariza Kaydini Duzenle';
        }

        return 'Oda Bakim/Ariza Detay';
    }

    get odaOptions(): SelectOption<number>[] {
        return this.odaSecenekleri
            .map((x) => ({
                label: `${x.odaNo} - ${x.binaAdi} (${x.odaTipiAdi})`,
                value: x.id
            }))
            .sort((left, right) => left.label.localeCompare(right.label));
    }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['model']) {
            this.applyModel(this.model);
        }

        if (changes['visible'] && this.visible) {
            this.applyModel(this.model);
        }
    }

    canSubmit(): boolean {
        const hasTesis = (this.selectedTesisId ?? 0) > 0;
        const hasRoom = (this.workingModel.odaId ?? 0) > 0;
        const hasType = (this.workingModel.blokTipi ?? '').trim().length > 0;
        if (!hasTesis || !hasRoom || !hasType || !this.baslangicTarihiInput || !this.bitisTarihiInput) {
            return false;
        }

        return new Date(this.baslangicTarihiInput).getTime() < new Date(this.bitisTarihiInput).getTime();
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            tesisId: this.selectedTesisId ?? 0,
            odaId: this.workingModel.odaId,
            blokTipi: this.workingModel.blokTipi,
            baslangicTarihi: this.toIsoDate(this.baslangicTarihiInput),
            bitisTarihi: this.toIsoDate(this.bitisTarihiInput),
            aciklama: this.normalizeOptional(this.workingModel.aciklama),
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

        this.applyModel(this.model);
        this.modeChange.emit('view');
    }

    close(): void {
        this.visibleChange.emit(false);
    }

    private applyModel(source: OdaKullanimBlokDto): void {
        this.workingModel = this.cloneModel(source);
        this.baslangicTarihiInput = this.toDateTimeLocalInput(source.baslangicTarihi);
        this.bitisTarihiInput = this.toDateTimeLocalInput(source.bitisTarihi);
    }

    private cloneModel(source: OdaKullanimBlokDto): OdaKullanimBlokDto {
        return {
            id: source.id ?? null,
            tesisId: source.tesisId ?? 0,
            odaId: source.odaId ?? 0,
            blokTipi: (source.blokTipi ?? 'Bakim').trim() || 'Bakim',
            baslangicTarihi: source.baslangicTarihi ?? '',
            bitisTarihi: source.bitisTarihi ?? '',
            aciklama: source.aciklama ?? null,
            aktifMi: source.aktifMi ?? true
        };
    }

    private emptyModel(): OdaKullanimBlokDto {
        return {
            tesisId: 0,
            odaId: 0,
            blokTipi: 'Bakim',
            baslangicTarihi: '',
            bitisTarihi: '',
            aciklama: null,
            aktifMi: true
        };
    }

    private toDateTimeLocalInput(value: string): string {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '';
        }

        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}`;
    }

    private toIsoDate(value: string): string {
        if (value.length === 16) {
            return `${value}:00`;
        }

        return value;
    }

    private normalizeOptional(value: string | null | undefined): string | null {
        const normalized = value?.trim() ?? '';
        return normalized.length > 0 ? normalized : null;
    }
}

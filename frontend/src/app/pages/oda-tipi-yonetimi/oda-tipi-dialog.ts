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
import { OdaOzellikDto, OdaOzellikVeriTipi } from '../oda-ozellik-yonetimi/oda-ozellik-yonetimi.dto';
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
            [style]="{ width: '46rem', 'max-width': '95vw' }"
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
                <div class="col-span-12 md:col-span-6 flex items-center gap-3 mt-2">
                    <p-toggleswitch inputId="paylasimliMi" [(ngModel)]="workingModel.paylasimliMi" [disabled]="isReadOnly || saving" />
                    <label for="paylasimliMi">Paylasimli</label>
                </div>
                <div class="col-span-12 md:col-span-6 flex items-center gap-3 mt-2">
                    <p-toggleswitch inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi">Aktif</label>
                </div>

                <div class="col-span-12">
                    <label class="block font-medium mb-2">Tip Varsayilan Ozellikleri</label>
                    @if (visibleOdaOzellikleri.length === 0) {
                        <span class="text-color-secondary">Tanimli aktif oda ozelligi bulunmuyor.</span>
                    } @else {
                        <div style="display: flex; flex-direction: column; gap: 0.75rem; border: 1px solid var(--surface-border); border-radius: 8px; padding: 0.75rem; background: var(--surface-ground); max-height: 22rem; overflow: auto;">
                            @for (group of groupedOdaOzellikleri; track group.key) {
                                <div style="display: flex; flex-direction: column; gap: 0.65rem;">
                                    <div style="display: flex; align-items: center; gap: 0.5rem; font-weight: 600;">
                                        <i [class]="group.icon" style="color: var(--primary-color);"></i>
                                        <span>{{ group.label }}</span>
                                        <span class="text-sm text-color-secondary">({{ group.items.length }})</span>
                                    </div>

                                    <div class="grid grid-cols-12 gap-3">
                                        @for (ozellik of group.items; track ozellik.id) {
                                            <div class="col-span-12 md:col-span-6">
                                                <div style="display: flex; flex-direction: column; gap: 0.45rem; padding: 0.55rem; border: 1px solid var(--surface-border); border-radius: 6px; background: var(--surface-card);">
                                                    <label class="font-medium">{{ ozellik.ad }}</label>

                                                    @if (ozellik.veriTipi === 'boolean') {
                                                        <p-select
                                                            [options]="booleanFeatureOptions"
                                                            optionLabel="label"
                                                            optionValue="value"
                                                            [ngModel]="getFeatureRawValue(ozellik.id ?? 0)"
                                                            (ngModelChange)="setFeatureRawValue(ozellik.id ?? 0, $event)"
                                                            appendTo="body"
                                                            class="w-full"
                                                            [disabled]="isReadOnly || saving"
                                                        />
                                                    } @else if (ozellik.veriTipi === 'number') {
                                                        <p-inputnumber
                                                            [ngModel]="getFeatureNumberValue(ozellik.id ?? 0)"
                                                            (ngModelChange)="setFeatureNumberValue(ozellik.id ?? 0, $event)"
                                                            mode="decimal"
                                                            [minFractionDigits]="0"
                                                            [maxFractionDigits]="2"
                                                            styleClass="w-full"
                                                            [disabled]="isReadOnly || saving"
                                                        />
                                                    } @else {
                                                        <input
                                                            pInputText
                                                            [ngModel]="getFeatureRawValue(ozellik.id ?? 0)"
                                                            (ngModelChange)="setFeatureRawValue(ozellik.id ?? 0, $event)"
                                                            class="w-full"
                                                            [placeholder]="ozellik.ad + ' varsayilani giriniz...'"
                                                            [disabled]="isReadOnly || saving"
                                                        />
                                                    }
                                                </div>
                                            </div>
                                        }
                                    </div>
                                </div>
                            }
                        </div>
                    }
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
    @Input() model: OdaTipiDto = { tesisId: 0, odaSinifiId: 0, ad: '', paylasimliMi: false, kapasite: 1, odaOzellikDegerleri: [], aktifMi: true };
    @Input() tesisler: TesisDto[] = [];
    @Input() odaSiniflari: OdaSinifiDto[] = [];
    @Input() odaOzellikleri: OdaOzellikDto[] = [];
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<OdaTipiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: OdaTipiDto = { tesisId: 0, odaSinifiId: 0, ad: '', paylasimliMi: false, kapasite: 1, odaOzellikDegerleri: [], aktifMi: true };
    readonly booleanFeatureOptions: Array<{ label: string; value: string | null }> = [
        { label: 'Belirtilmedi', value: null },
        { label: 'Evet', value: 'true' },
        { label: 'Hayir', value: 'false' }
    ];
    private readonly featureGroupDefinitions: Array<{ key: OdaOzellikVeriTipi; label: string; icon: string }> = [
        { key: 'boolean', label: 'Secim Ozellikleri', icon: 'pi pi-check-square' },
        { key: 'number', label: 'Sayisal Ozellikler', icon: 'pi pi-hashtag' },
        { key: 'text', label: 'Metin Ozellikleri', icon: 'pi pi-align-left' }
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
            return 'Yeni Oda Tipi';
        }

        if (this.mode === 'edit') {
            return 'Oda Tipi Duzenle';
        }

        return 'Oda Tipi Detay';
    }

    get visibleOdaOzellikleri(): OdaOzellikDto[] {
        const selectedFeatureIds = new Set((this.workingModel.odaOzellikDegerleri ?? []).map((item) => item.odaOzellikId));
        return [...this.odaOzellikleri]
            .filter((item) => item.aktifMi || selectedFeatureIds.has(item.id ?? 0))
            .sort((left, right) => (left.ad ?? '').localeCompare(right.ad ?? ''));
    }

    get groupedOdaOzellikleri(): Array<{ key: OdaOzellikVeriTipi; label: string; icon: string; items: OdaOzellikDto[] }> {
        return this.featureGroupDefinitions
            .map((group) => ({
                ...group,
                items: this.visibleOdaOzellikleri.filter((item) => item.veriTipi === group.key)
            }))
            .filter((group) => group.items.length > 0);
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
            odaOzellikDegerleri: this.getSanitizedFeatureValues(),
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

    getFeatureRawValue(ozellikId: number): string | null {
        const value = this.workingModel.odaOzellikDegerleri.find((item) => item.odaOzellikId === ozellikId);
        const normalized = value?.deger?.trim();
        return normalized && normalized.length > 0 ? normalized : null;
    }

    setFeatureRawValue(ozellikId: number, rawValue: string | null | undefined): void {
        const value = typeof rawValue === 'string' ? rawValue.trim() : null;
        this.upsertFeatureValue(ozellikId, value && value.length > 0 ? value : null);
    }

    getFeatureNumberValue(ozellikId: number): number | null {
        const rawValue = this.getFeatureRawValue(ozellikId);
        if (!rawValue) {
            return null;
        }

        const parsed = Number(rawValue);
        return Number.isFinite(parsed) ? parsed : null;
    }

    setFeatureNumberValue(ozellikId: number, value: number | null | undefined): void {
        if (value === null || value === undefined) {
            this.upsertFeatureValue(ozellikId, null);
            return;
        }

        this.upsertFeatureValue(ozellikId, value.toString());
    }

    private upsertFeatureValue(ozellikId: number, value: string | null): void {
        const existing = this.workingModel.odaOzellikDegerleri.find((item) => item.odaOzellikId === ozellikId);
        if (!value) {
            if (existing) {
                this.workingModel.odaOzellikDegerleri = this.workingModel.odaOzellikDegerleri.filter((item) => item.odaOzellikId !== ozellikId);
            }
            return;
        }

        if (existing) {
            existing.deger = value;
            return;
        }

        this.workingModel.odaOzellikDegerleri = [
            ...(this.workingModel.odaOzellikDegerleri ?? []),
            { odaOzellikId: ozellikId, deger: value }
        ];
    }

    private getSanitizedFeatureValues(): Array<{ odaOzellikId: number; deger: string }> {
        const values = this.workingModel.odaOzellikDegerleri ?? [];
        const uniqueByFeatureId = new Map<number, string>();

        values.forEach((item) => {
            const featureId = item.odaOzellikId;
            const value = item.deger?.trim() ?? '';
            if (!featureId || value.length === 0) {
                return;
            }

            uniqueByFeatureId.set(featureId, value);
        });

        return Array.from(uniqueByFeatureId.entries()).map(([odaOzellikId, deger]) => ({ odaOzellikId, deger }));
    }

    private cloneModel(source: OdaTipiDto): OdaTipiDto {
        return {
            ...source,
            odaOzellikDegerleri: (source.odaOzellikDegerleri ?? []).map((item) => ({ ...item }))
        };
    }
}

import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ManagerCandidateDto } from '../../core/identity';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { IlDto } from '../il-yonetimi/il-yonetimi.dto';
import { TesisDto } from './tesis-yonetimi.dto';

@Component({
    selector: 'app-tesis-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, MultiSelectModule, SelectModule, ToggleSwitchModule],
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
                    <label for="ad" class="block font-medium mb-2">Tesis Adi</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="ilId" class="block font-medium mb-2">Il</label>
                    <p-select
                        inputId="ilId"
                        [options]="iller"
                        optionLabel="ad"
                        optionValue="id"
                        [(ngModel)]="workingModel.ilId"
                        [showClear]="true"
                        [filter]="true"
                        appendTo="body"
                        class="w-full"
                        [disabled]="isReadOnly || saving"
                    />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="telefon" class="block font-medium mb-2">Telefon</label>
                    <input id="telefon" pInputText [(ngModel)]="workingModel.telefon" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="eposta" class="block font-medium mb-2">Eposta</label>
                    <input id="eposta" pInputText [(ngModel)]="workingModel.eposta" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-3">
                    <label for="girisSaati" class="block font-medium mb-2">Giris Saati</label>
                    <input id="girisSaati" pInputText type="time" [(ngModel)]="workingModel.girisSaati" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-3">
                    <label for="cikisSaati" class="block font-medium mb-2">Cikis Saati</label>
                    <input id="cikisSaati" pInputText type="time" [(ngModel)]="workingModel.cikisSaati" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12">
                    <label for="adres" class="block font-medium mb-2">Adres</label>
                    <input id="adres" pInputText [(ngModel)]="workingModel.adres" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 flex items-center gap-3">
                    <p-toggleswitch inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi">Aktif</label>
                </div>
                @if (canManage && canAssignTesisYoneticisi) {
                    <div class="col-span-12">
                        <label for="yoneticiUserIds" class="block font-medium mb-2">Tesis Yoneticileri</label>
                        <p-multiselect
                            inputId="yoneticiUserIds"
                            [options]="yoneticiSecenekleri"
                            optionLabel="label"
                            optionValue="value"
                            [(ngModel)]="workingModel.yoneticiUserIds"
                            [showClear]="true"
                            [filter]="true"
                            appendTo="body"
                            placeholder="Yoneticileri secin"
                            class="w-full"
                            [disabled]="isReadOnly || saving"
                        />
                    </div>
                }
                @if (canManage && canAssignResepsiyonist) {
                    <div class="col-span-12">
                        <label for="resepsiyonistUserIds" class="block font-medium mb-2">Resepsiyonistler</label>
                        <p-multiselect
                            inputId="resepsiyonistUserIds"
                            [options]="resepsiyonistSecenekleri"
                            optionLabel="label"
                            optionValue="value"
                            [(ngModel)]="workingModel.resepsiyonistUserIds"
                            [showClear]="true"
                            [filter]="true"
                            appendTo="body"
                            placeholder="Resepsiyonistleri secin"
                            class="w-full"
                            [disabled]="isReadOnly || saving"
                        />
                    </div>
                }
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
export class TesisDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: TesisDto = { ad: '', ilId: 0, telefon: '', adres: '', eposta: null, girisSaati: '14:00', cikisSaati: '10:00', aktifMi: true, yoneticiUserIds: null, resepsiyonistUserIds: null };
    @Input() iller: IlDto[] = [];
    @Input() yoneticiAdaylari: ManagerCandidateDto[] = [];
    @Input() resepsiyonistAdaylari: ManagerCandidateDto[] = [];
    @Input() saving = false;
    @Input() canManage = false;
    @Input() canAssignTesisYoneticisi = false;
    @Input() canAssignResepsiyonist = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<TesisDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: TesisDto = { ad: '', ilId: 0, telefon: '', adres: '', eposta: null, girisSaati: '14:00', cikisSaati: '10:00', aktifMi: true, yoneticiUserIds: null, resepsiyonistUserIds: null };

    get yoneticiSecenekleri(): Array<{ label: string; value: string }> {
        return this.yoneticiAdaylari.map((item) => ({
            value: item.id,
            label: item.adSoyad && item.adSoyad.trim().length > 0
                ? `${item.userName} - ${item.adSoyad}`
                : item.userName
        }));
    }

    get resepsiyonistSecenekleri(): Array<{ label: string; value: string }> {
        return this.resepsiyonistAdaylari.map((item) => ({
            value: item.id,
            label: item.adSoyad && item.adSoyad.trim().length > 0
                ? `${item.userName} - ${item.adSoyad}`
                : item.userName
        }));
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
            return 'Yeni Tesis';
        }

        if (this.mode === 'edit') {
            return 'Tesis Duzenle';
        }

        return 'Tesis Detay';
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
        return (this.workingModel.ad?.trim() ?? '').length > 0
            && !!this.workingModel.ilId
            && (this.workingModel.telefon?.trim() ?? '').length > 0
            && (this.workingModel.adres?.trim() ?? '').length > 0
            && (this.workingModel.girisSaati?.trim() ?? '').length > 0
            && (this.workingModel.cikisSaati?.trim() ?? '').length > 0;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            ad: this.workingModel.ad.trim(),
            ilId: this.workingModel.ilId,
            telefon: this.workingModel.telefon.trim(),
            adres: this.workingModel.adres.trim(),
            eposta: this.workingModel.eposta?.trim() || null,
            girisSaati: this.normalizeSaat(this.workingModel.girisSaati, '14:00'),
            cikisSaati: this.normalizeSaat(this.workingModel.cikisSaati, '10:00'),
            aktifMi: this.workingModel.aktifMi,
            yoneticiUserIds: this.canAssignTesisYoneticisi ? this.workingModel.yoneticiUserIds ?? [] : null,
            resepsiyonistUserIds: this.canAssignResepsiyonist ? this.workingModel.resepsiyonistUserIds ?? [] : null
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

    private cloneModel(model: TesisDto): TesisDto {
        return {
            ...model,
            girisSaati: this.normalizeSaat(model.girisSaati, '14:00'),
            cikisSaati: this.normalizeSaat(model.cikisSaati, '10:00'),
            yoneticiUserIds: [...(model.yoneticiUserIds ?? [])],
            resepsiyonistUserIds: [...(model.resepsiyonistUserIds ?? [])]
        };
    }

    private normalizeSaat(value: string | null | undefined, fallback: string): string {
        const normalized = (value ?? '').trim();
        if (/^\d{2}:\d{2}$/.test(normalized)) {
            return normalized;
        }

        return fallback;
    }
}

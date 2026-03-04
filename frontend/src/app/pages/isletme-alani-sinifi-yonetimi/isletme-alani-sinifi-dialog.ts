import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { IsletmeAlaniSinifiDto } from '../isletme-alani-yonetimi/isletme-alani-yonetimi.dto';

@Component({
    selector: 'app-isletme-alani-sinifi-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, ToggleSwitchModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '36rem', 'max-width': '95vw' }"
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
                    <label for="kod" class="block font-medium mb-2">Kod</label>
                    <input id="kod" pInputText [(ngModel)]="workingModel.kod" class="w-full" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" />
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
export class IsletmeAlaniSinifiDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: IsletmeAlaniSinifiDto = { kod: '', ad: '', aktifMi: true };
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<IsletmeAlaniSinifiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: IsletmeAlaniSinifiDto = { kod: '', ad: '', aktifMi: true };

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
            return 'Yeni Isletme Alani Sinifi';
        }

        if (this.mode === 'edit') {
            return 'Isletme Alani Sinifi Duzenle';
        }

        return 'Isletme Alani Sinifi Detay';
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
        return (this.workingModel.kod?.trim() ?? '').length > 0
            && (this.workingModel.ad?.trim() ?? '').length > 0;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
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

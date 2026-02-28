import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { UlkeDto } from './ulke-yonetimi.dto';

@Component({
    selector: 'app-ulke-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '30rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '90vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12">
                    <label for="code" class="block font-medium mb-2">Ulke Kodu</label>
                    <input id="code" pInputText [(ngModel)]="workingModel.code" class="w-full" [disabled]="isReadOnly || saving" maxlength="16" />
                </div>
                <div class="col-span-12">
                    <label for="name" class="block font-medium mb-2">Ulke Adi</label>
                    <input id="name" pInputText [(ngModel)]="workingModel.name" class="w-full" [disabled]="isReadOnly || saving" maxlength="128" />
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
export class UlkeDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: UlkeDto = { name: '', code: '' };
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<UlkeDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: UlkeDto = { name: '', code: '' };

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
            return 'Yeni Ulke';
        }

        if (this.mode === 'edit') {
            return 'Ulke Duzenle';
        }

        return 'Ulke Detay';
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
        const name = this.workingModel.name?.trim() ?? '';
        const code = this.workingModel.code?.trim() ?? '';
        return name.length > 0 && code.length > 0;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            name: this.workingModel.name.trim(),
            code: this.workingModel.code.trim().toUpperCase()
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

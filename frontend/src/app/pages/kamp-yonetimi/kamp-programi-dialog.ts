import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { KampProgramiDto } from './kamp-yonetimi.dto';

@Component({
    selector: 'app-kamp-programi-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, CheckboxModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '34rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '90vw' }"
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
                <div class="col-span-12 md:col-span-5">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" maxlength="128" />
                </div>
                <div class="col-span-12 md:col-span-3">
                    <label for="yil" class="block font-medium mb-2">Yil</label>
                    <input id="yil" pInputText [(ngModel)]="workingModel.yil" class="w-full" [disabled]="isReadOnly || saving" type="number" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="maksimumBasvuruSayisi" class="block font-medium mb-2">Maks. Basvuru (Kisi Basi)</label>
                    <input id="maksimumBasvuruSayisi" pInputText [(ngModel)]="workingModel.maksimumBasvuruSayisi" class="w-full" [disabled]="isReadOnly || saving" type="number" min="1" max="20" />
                </div>
                <div class="col-span-12">
                    <label for="aciklama" class="block font-medium mb-2">Aciklama</label>
                    <input id="aciklama" pInputText [(ngModel)]="workingModel.aciklama" class="w-full" [disabled]="isReadOnly || saving" maxlength="512" />
                </div>
                <div class="col-span-12">
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
export class KampProgramiDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: KampProgramiDto = { kod: '', ad: '', aciklama: null, yil: new Date().getFullYear(), maksimumBasvuruSayisi: 1, aktifMi: true };
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<KampProgramiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: KampProgramiDto = { kod: '', ad: '', aciklama: null, yil: new Date().getFullYear(), maksimumBasvuruSayisi: 1, aktifMi: true };

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
            return 'Yeni Kamp Programi';
        }

        if (this.mode === 'edit') {
            return 'Kamp Programi Duzenle';
        }

        return 'Kamp Programi Detay';
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
        return (this.workingModel.kod?.trim().length ?? 0) > 0
            && (this.workingModel.ad?.trim().length ?? 0) > 0
            && Number(this.workingModel.maksimumBasvuruSayisi) >= 1
            && Number(this.workingModel.maksimumBasvuruSayisi) <= 20;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
            aciklama: this.workingModel.aciklama?.trim() || null,
            yil: this.workingModel.yil,
            maksimumBasvuruSayisi: Number(this.workingModel.maksimumBasvuruSayisi),
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

import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { KampDonemiDto, KampProgramiSecenekDto } from './kamp-yonetimi.dto';

@Component({
    selector: 'app-kamp-donemi-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, CheckboxModule, SelectModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '52rem', 'max-width': '95vw' }"
            [breakpoints]="{ '960px': '94vw' }"
            (onHide)="close()"
        >
            @if (showLockToggle) {
                <div class="flex justify-end mb-3">
                    <p-button [icon]="lockIcon" size="small" [severity]="lockSeverity" [rounded]="true" [outlined]="false" [ariaLabel]="lockAriaLabel" styleClass="shadow-2" [disabled]="saving" (onClick)="toggleLockMode()" />
                </div>
            }

            <div class="grid grid-cols-12 gap-4">
                <div class="col-span-12 md:col-span-8">
                    <label for="kampProgramiId" class="block font-medium mb-2">Kamp Programi</label>
                    <p-select inputId="kampProgramiId" class="w-full" [options]="programOptions" optionLabel="label" optionValue="value" [(ngModel)]="workingModel.kampProgramiId" [appendTo]="'body'" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="kod" class="block font-medium mb-2">Kod</label>
                    <input id="kod" pInputText [(ngModel)]="workingModel.kod" class="w-full" [disabled]="isReadOnly || saving" maxlength="64" />
                </div>
                <div class="col-span-12">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" maxlength="160" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="basvuruBaslangic" class="block font-medium mb-2">Basvuru Baslangic</label>
                    <input id="basvuruBaslangic" pInputText type="date" class="w-full" [(ngModel)]="workingModel.basvuruBaslangicTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="basvuruBitis" class="block font-medium mb-2">Basvuru Bitis</label>
                    <input id="basvuruBitis" pInputText type="date" class="w-full" [(ngModel)]="workingModel.basvuruBitisTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="konaklamaBaslangic" class="block font-medium mb-2">Konaklama Baslangic</label>
                    <input id="konaklamaBaslangic" pInputText type="date" class="w-full" [(ngModel)]="workingModel.konaklamaBaslangicTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="konaklamaBitis" class="block font-medium mb-2">Konaklama Bitis</label>
                    <input id="konaklamaBitis" pInputText type="date" class="w-full" [(ngModel)]="workingModel.konaklamaBitisTarihi" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="minimumGece" class="block font-medium mb-2">Minimum Gece</label>
                    <input id="minimumGece" pInputText type="number" min="1" class="w-full" [(ngModel)]="workingModel.minimumGece" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="maksimumGece" class="block font-medium mb-2">Maksimum Gece</label>
                    <input id="maksimumGece" pInputText type="number" min="1" class="w-full" [(ngModel)]="workingModel.maksimumGece" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-4">
                    <label for="iptalSonGun" class="block font-medium mb-2">Iptal Son Gun</label>
                    <input id="iptalSonGun" pInputText type="date" class="w-full" [(ngModel)]="workingModel.iptalSonGun" [disabled]="isReadOnly || saving" />
                </div>
                <div class="col-span-12 md:col-span-3 flex items-center">
                    <p-checkbox inputId="onayGerektirirMi" [(ngModel)]="workingModel.onayGerektirirMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="onayGerektirirMi" class="ml-2">Onay Gerektirir</label>
                </div>
                <div class="col-span-12 md:col-span-3 flex items-center">
                    <p-checkbox inputId="cekilisGerekliMi" [(ngModel)]="workingModel.cekilisGerekliMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="cekilisGerekliMi" class="ml-2">Cekilis Gerekli</label>
                </div>
                <div class="col-span-12 md:col-span-3 flex items-center">
                    <p-checkbox inputId="ayniAileIcinTekBasvuruMu" [(ngModel)]="workingModel.ayniAileIcinTekBasvuruMu" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="ayniAileIcinTekBasvuruMu" class="ml-2">Ayni Aile Tek Basvuru</label>
                </div>
                <div class="col-span-12 md:col-span-3 flex items-center">
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
export class KampDonemiDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: KampDonemiDto = this.emptyModel();
    @Input() saving = false;
    @Input() canManage = false;
    @Input() programlar: KampProgramiSecenekDto[] = [];

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<KampDonemiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: KampDonemiDto = this.emptyModel();

    get programOptions(): Array<{ label: string; value: number }> {
        return this.programlar.map((item) => ({ label: `${item.yil} - ${item.ad}`, value: item.id }));
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
            return 'Yeni Kamp Donemi';
        }

        if (this.mode === 'edit') {
            return 'Kamp Donemi Duzenle';
        }

        return 'Kamp Donemi Detay';
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
        const hasProgram = (this.workingModel.kampProgramiId ?? 0) > 0;
        const hasKod = (this.workingModel.kod?.trim().length ?? 0) > 0;
        const hasAd = (this.workingModel.ad?.trim().length ?? 0) > 0;
        const validMin = Number(this.workingModel.minimumGece) >= 1;
        const validMax = Number(this.workingModel.maksimumGece) >= Number(this.workingModel.minimumGece);
        const basvuruOk = !!this.workingModel.basvuruBaslangicTarihi && !!this.workingModel.basvuruBitisTarihi && this.workingModel.basvuruBaslangicTarihi <= this.workingModel.basvuruBitisTarihi;
        const konaklamaOk = !!this.workingModel.konaklamaBaslangicTarihi && !!this.workingModel.konaklamaBitisTarihi && this.workingModel.konaklamaBaslangicTarihi <= this.workingModel.konaklamaBitisTarihi;
        const iptalOk = !this.workingModel.iptalSonGun || this.workingModel.iptalSonGun <= this.workingModel.konaklamaBaslangicTarihi;

        return hasProgram && hasKod && hasAd && validMin && validMax && basvuruOk && konaklamaOk && iptalOk;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            ...this.workingModel,
            id: this.workingModel.id ?? null,
            kampProgramiId: Number(this.workingModel.kampProgramiId),
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
            minimumGece: Number(this.workingModel.minimumGece),
            maksimumGece: Number(this.workingModel.maksimumGece),
            iptalSonGun: this.workingModel.iptalSonGun || null
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

    private emptyModel(): KampDonemiDto {
        return {
            kampProgramiId: 0,
            kod: '',
            ad: '',
            basvuruBaslangicTarihi: '',
            basvuruBitisTarihi: '',
            konaklamaBaslangicTarihi: '',
            konaklamaBitisTarihi: '',
            minimumGece: 1,
            maksimumGece: 1,
            onayGerektirirMi: true,
            cekilisGerekliMi: false,
            ayniAileIcinTekBasvuruMu: true,
            iptalSonGun: null,
            aktifMi: true
        };
    }
}

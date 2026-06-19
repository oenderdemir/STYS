import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { parseApiDate, toLocalDateString } from '../../core/utils/date-time.util';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import { KampDonemiDto, KampProgramiSecenekDto } from './kamp-yonetimi.dto';

interface KampDonemiWorkingModel {
    id?: number | null;
    kampProgramiId: number;
    kod: string;
    ad: string;
    basvuruBaslangicTarihi: Date | null;
    basvuruBitisTarihi: Date | null;
    konaklamaBaslangicTarihi: Date | null;
    konaklamaBitisTarihi: Date | null;
    minimumGece: number;
    maksimumGece: number;
    onayGerektirirMi: boolean;
    cekilisGerekliMi: boolean;
    ayniAileIcinTekBasvuruMu: boolean;
    iptalSonGun: Date | null;
    aktifMi: boolean;
}

@Component({
    selector: 'app-kamp-donemi-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, DatePickerModule, InputTextModule, CheckboxModule, SelectModule],
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
                    <p-datepicker id="basvuruBaslangic" class="w-full" styleClass="w-full" inputStyleClass="w-full" [(ngModel)]="workingModel.basvuruBaslangicTarihi" [disabled]="isReadOnly || saving" dateFormat="dd.mm.yy" [firstDayOfWeek]="1" [showIcon]="true" [showButtonBar]="true" appendTo="body" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="basvuruBitis" class="block font-medium mb-2">Basvuru Bitis</label>
                    <p-datepicker id="basvuruBitis" class="w-full" styleClass="w-full" inputStyleClass="w-full" [(ngModel)]="workingModel.basvuruBitisTarihi" [disabled]="isReadOnly || saving" dateFormat="dd.mm.yy" [firstDayOfWeek]="1" [showIcon]="true" [showButtonBar]="true" appendTo="body" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="konaklamaBaslangic" class="block font-medium mb-2">Konaklama Baslangic</label>
                    <p-datepicker id="konaklamaBaslangic" class="w-full" styleClass="w-full" inputStyleClass="w-full" [(ngModel)]="workingModel.konaklamaBaslangicTarihi" [disabled]="isReadOnly || saving" dateFormat="dd.mm.yy" [firstDayOfWeek]="1" [showIcon]="true" [showButtonBar]="true" appendTo="body" />
                </div>
                <div class="col-span-12 md:col-span-6">
                    <label for="konaklamaBitis" class="block font-medium mb-2">Konaklama Bitis</label>
                    <p-datepicker id="konaklamaBitis" class="w-full" styleClass="w-full" inputStyleClass="w-full" [(ngModel)]="workingModel.konaklamaBitisTarihi" [disabled]="isReadOnly || saving" dateFormat="dd.mm.yy" [firstDayOfWeek]="1" [showIcon]="true" [showButtonBar]="true" appendTo="body" />
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
                    <p-datepicker id="iptalSonGun" class="w-full" styleClass="w-full" inputStyleClass="w-full" [(ngModel)]="workingModel.iptalSonGun" [disabled]="isReadOnly || saving" dateFormat="dd.mm.yy" [firstDayOfWeek]="1" [showIcon]="true" [showButtonBar]="true" appendTo="body" />
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

    workingModel: KampDonemiWorkingModel = this.emptyWorkingModel();

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
            this.workingModel = this.toWorkingModel(this.model);
        }

        if (changes['visible'] && this.visible) {
            this.workingModel = this.toWorkingModel(this.model);
        }
    }

    canSubmit(): boolean {
        const hasProgram = (this.workingModel.kampProgramiId ?? 0) > 0;
        const hasKod = (this.workingModel.kod?.trim().length ?? 0) > 0;
        const hasAd = (this.workingModel.ad?.trim().length ?? 0) > 0;
        const validMin = Number(this.workingModel.minimumGece) >= 1;
        const validMax = Number(this.workingModel.maksimumGece) >= Number(this.workingModel.minimumGece);

        const bbs = this.workingModel.basvuruBaslangicTarihi;
        const bbt = this.workingModel.basvuruBitisTarihi;
        const kbs = this.workingModel.konaklamaBaslangicTarihi;
        const kbt = this.workingModel.konaklamaBitisTarihi;
        const isg = this.workingModel.iptalSonGun;

        const basvuruOk = !!bbs && !!bbt && bbs.getTime() <= bbt.getTime();
        const konaklamaOk = !!kbs && !!kbt && kbs.getTime() <= kbt.getTime();
        const iptalOk = !isg || (!!kbs && isg.getTime() <= kbs.getTime());

        return hasProgram && hasKod && hasAd && validMin && validMax && basvuruOk && konaklamaOk && iptalOk;
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            ...this.model,
            id: this.workingModel.id ?? null,
            kampProgramiId: Number(this.workingModel.kampProgramiId),
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
            basvuruBaslangicTarihi: toLocalDateString(this.workingModel.basvuruBaslangicTarihi) ?? '',
            basvuruBitisTarihi: toLocalDateString(this.workingModel.basvuruBitisTarihi) ?? '',
            konaklamaBaslangicTarihi: toLocalDateString(this.workingModel.konaklamaBaslangicTarihi) ?? '',
            konaklamaBitisTarihi: toLocalDateString(this.workingModel.konaklamaBitisTarihi) ?? '',
            minimumGece: Number(this.workingModel.minimumGece),
            maksimumGece: Number(this.workingModel.maksimumGece),
            onayGerektirirMi: this.workingModel.onayGerektirirMi,
            cekilisGerekliMi: this.workingModel.cekilisGerekliMi,
            ayniAileIcinTekBasvuruMu: this.workingModel.ayniAileIcinTekBasvuruMu,
            iptalSonGun: toLocalDateString(this.workingModel.iptalSonGun),
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

        this.workingModel = this.toWorkingModel(this.model);
        this.modeChange.emit('view');
    }

    close(): void {
        this.visibleChange.emit(false);
    }

    private toWorkingModel(source: KampDonemiDto): KampDonemiWorkingModel {
        return {
            id: source.id ?? null,
            kampProgramiId: source.kampProgramiId,
            kod: source.kod ?? '',
            ad: source.ad ?? '',
            basvuruBaslangicTarihi: parseApiDate(source.basvuruBaslangicTarihi),
            basvuruBitisTarihi: parseApiDate(source.basvuruBitisTarihi),
            konaklamaBaslangicTarihi: parseApiDate(source.konaklamaBaslangicTarihi),
            konaklamaBitisTarihi: parseApiDate(source.konaklamaBitisTarihi),
            minimumGece: source.minimumGece ?? 1,
            maksimumGece: source.maksimumGece ?? 1,
            onayGerektirirMi: !!source.onayGerektirirMi,
            cekilisGerekliMi: !!source.cekilisGerekliMi,
            ayniAileIcinTekBasvuruMu: source.ayniAileIcinTekBasvuruMu ?? true,
            iptalSonGun: parseApiDate(source.iptalSonGun ?? null),
            aktifMi: source.aktifMi ?? true
        };
    }

    private emptyWorkingModel(): KampDonemiWorkingModel {
        return {
            kampProgramiId: 0,
            kod: '',
            ad: '',
            basvuruBaslangicTarihi: null,
            basvuruBitisTarihi: null,
            konaklamaBaslangicTarihi: null,
            konaklamaBitisTarihi: null,
            minimumGece: 1,
            maksimumGece: 1,
            onayGerektirirMi: true,
            cekilisGerekliMi: false,
            ayniAileIcinTekBasvuruMu: true,
            iptalSonGun: null,
            aktifMi: true
        };
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

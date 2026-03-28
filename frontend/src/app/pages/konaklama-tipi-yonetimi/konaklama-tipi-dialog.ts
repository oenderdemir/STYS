import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { CrudDialogMode } from '../../core/ui/crud-dialog-mode.type';
import {
    KonaklamaTipiIcerikHizmetSecenekleri,
    KonaklamaTipiIcerikKullanimNoktasiSecenekleri,
    KonaklamaTipiIcerikKullanimTipiSecenekleri,
    KonaklamaTipiIcerikPeriyotSecenekleri
} from './konaklama-tipi-icerik.constants';
import { KonaklamaTipiDto, KonaklamaTipiIcerikDto } from './konaklama-tipi-yonetimi.dto';

@Component({
    selector: 'app-konaklama-tipi-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, DialogModule, ButtonModule, InputTextModule, CheckboxModule, SelectModule, InputNumberModule, TextareaModule],
    template: `
        <p-dialog
            [header]="dialogTitle"
            [visible]="visible"
            [modal]="true"
            [style]="{ width: '52rem', 'max-width': '96vw' }"
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
                <div class="col-span-12 md:col-span-8">
                    <label for="ad" class="block font-medium mb-2">Ad</label>
                    <input id="ad" pInputText [(ngModel)]="workingModel.ad" class="w-full" [disabled]="isReadOnly || saving" maxlength="128" />
                </div>
                <div class="col-span-12">
                    <p-checkbox inputId="aktifMi" [(ngModel)]="workingModel.aktifMi" [binary]="true" [disabled]="isReadOnly || saving" />
                    <label for="aktifMi" class="ml-2">Aktif</label>
                </div>

                <div class="col-span-12">
                    <div class="flex items-center justify-between mb-2">
                        <div>
                            <div class="font-medium">Paket Icerigi</div>
                            <div class="text-sm text-color-secondary">Bu konaklama tipinin icine dahil olan hizmetleri acikca tanimlayin.</div>
                        </div>
                        @if (!isReadOnly) {
                            <p-button label="Icerik Ekle" icon="pi pi-plus" size="small" [disabled]="saving" (onClick)="addIcerikKalemi()" />
                        }
                    </div>

                    @if (workingModel.icerikKalemleri.length === 0) {
                        <div class="border-1 border-200 border-round p-3 text-sm text-color-secondary">
                            Bu paket icin henuz icerik tanimlanmadi. Ornegin Oda Kahvalti icin Kahvalti ekleyebilirsin.
                        </div>
                    } @else {
                        <div class="flex flex-column gap-3">
                            @for (item of workingModel.icerikKalemleri; track trackIcerikKalemi($index, item)) {
                                <div class="border-1 border-200 border-round p-3">
                                    <div class="grid grid-cols-12 gap-3">
                                        <div class="col-span-12 md:col-span-4">
                                            <label class="block font-medium mb-2">Hizmet</label>
                                            <p-select
                                                [options]="hizmetSecenekleri"
                                                optionLabel="label"
                                                optionValue="value"
                                                [(ngModel)]="item.hizmetKodu"
                                                [disabled]="isReadOnly || saving"
                                                [appendTo]="'body'"
                                                class="w-full"
                                                placeholder="Hizmet seciniz"
                                                (ngModelChange)="syncIcerikAdlari(item)"
                                            />
                                        </div>
                                        <div class="col-span-12 md:col-span-3">
                                            <label class="block font-medium mb-2">Miktar</label>
                                            <p-inputnumber [(ngModel)]="item.miktar" [min]="1" [useGrouping]="false" [disabled]="isReadOnly || saving" styleClass="w-full" />
                                        </div>
                                        <div class="col-span-12 md:col-span-4">
                                            <label class="block font-medium mb-2">Periyot</label>
                                            <p-select
                                                [options]="periyotSecenekleri"
                                                optionLabel="label"
                                                optionValue="value"
                                                [(ngModel)]="item.periyot"
                                                [disabled]="isReadOnly || saving"
                                                [appendTo]="'body'"
                                                class="w-full"
                                                placeholder="Periyot seciniz"
                                                (ngModelChange)="syncIcerikAdlari(item)"
                                            />
                                        </div>
                                        <div class="col-span-12 md:col-span-1 flex md:justify-end md:items-end">
                                            @if (!isReadOnly) {
                                                <p-button icon="pi pi-trash" severity="danger" [outlined]="true" [disabled]="saving" (onClick)="removeIcerikKalemi($index)" />
                                            }
                                        </div>
                                        <div class="col-span-12 md:col-span-4">
                                            <label class="block font-medium mb-2">Kullanim Tipi</label>
                                            <p-select
                                                [options]="kullanimTipiSecenekleri"
                                                optionLabel="label"
                                                optionValue="value"
                                                [(ngModel)]="item.kullanimTipi"
                                                [disabled]="isReadOnly || saving"
                                                [appendTo]="'body'"
                                                class="w-full"
                                                placeholder="Kullanim tipi seciniz"
                                                (ngModelChange)="syncIcerikAdlari(item)"
                                            />
                                        </div>
                                        <div class="col-span-12 md:col-span-4">
                                            <label class="block font-medium mb-2">Kullanim Noktasi</label>
                                            <p-select
                                                [options]="kullanimNoktasiSecenekleri"
                                                optionLabel="label"
                                                optionValue="value"
                                                [(ngModel)]="item.kullanimNoktasi"
                                                [disabled]="isReadOnly || saving"
                                                [appendTo]="'body'"
                                                class="w-full"
                                                placeholder="Kullanim noktasi seciniz"
                                                (ngModelChange)="syncIcerikAdlari(item)"
                                            />
                                        </div>
                                        <div class="col-span-12 md:col-span-2">
                                            <label class="block font-medium mb-2">Baslangic</label>
                                            <input pInputText type="time" class="w-full" [(ngModel)]="item.kullanimBaslangicSaati" [disabled]="isReadOnly || saving" />
                                        </div>
                                        <div class="col-span-12 md:col-span-2">
                                            <label class="block font-medium mb-2">Bitis</label>
                                            <input pInputText type="time" class="w-full" [(ngModel)]="item.kullanimBitisSaati" [disabled]="isReadOnly || saving" />
                                        </div>
                                        <div class="col-span-12 md:col-span-6 flex flex-wrap gap-4 items-end">
                                            <div>
                                                <p-checkbox [inputId]="'checkin-' + $index" [(ngModel)]="item.checkInGunuGecerliMi" [binary]="true" [disabled]="isReadOnly || saving" />
                                                <label [for]="'checkin-' + $index" class="ml-2">Check-in gunu gecerli</label>
                                            </div>
                                            <div>
                                                <p-checkbox [inputId]="'checkout-' + $index" [(ngModel)]="item.checkOutGunuGecerliMi" [binary]="true" [disabled]="isReadOnly || saving" />
                                                <label [for]="'checkout-' + $index" class="ml-2">Check-out gunu gecerli</label>
                                            </div>
                                        </div>
                                        <div class="col-span-12">
                                            <label class="block font-medium mb-2">Aciklama</label>
                                            <textarea pInputTextarea rows="2" class="w-full" [(ngModel)]="item.aciklama" [disabled]="isReadOnly || saving" maxlength="256"></textarea>
                                        </div>
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
export class KonaklamaTipiDialog implements OnChanges {
    @Input() visible = false;
    @Input() mode: CrudDialogMode = 'create';
    @Input() model: KonaklamaTipiDto = { kod: '', ad: '', aktifMi: true, icerikKalemleri: [] };
    @Input() saving = false;
    @Input() canManage = false;

    @Output() readonly visibleChange = new EventEmitter<boolean>();
    @Output() readonly save = new EventEmitter<KonaklamaTipiDto>();
    @Output() readonly modeChange = new EventEmitter<CrudDialogMode>();

    workingModel: KonaklamaTipiDto = { kod: '', ad: '', aktifMi: true, icerikKalemleri: [] };
    readonly hizmetSecenekleri = KonaklamaTipiIcerikHizmetSecenekleri;
    readonly periyotSecenekleri = KonaklamaTipiIcerikPeriyotSecenekleri;
    readonly kullanimTipiSecenekleri = KonaklamaTipiIcerikKullanimTipiSecenekleri;
    readonly kullanimNoktasiSecenekleri = KonaklamaTipiIcerikKullanimNoktasiSecenekleri;

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
            return 'Yeni Konaklama Tipi';
        }

        if (this.mode === 'edit') {
            return 'Konaklama Tipi Duzenle';
        }

        return 'Konaklama Tipi Detay';
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
        return kod.length > 0 && ad.length > 0
            && this.workingModel.icerikKalemleri.every((item) =>
                (item.hizmetKodu?.trim().length ?? 0) > 0
                && (item.periyot?.trim().length ?? 0) > 0
                && (item.kullanimTipi?.trim().length ?? 0) > 0
                && (item.kullanimNoktasi?.trim().length ?? 0) > 0
                && (item.miktar ?? 0) > 0);
    }

    submit(): void {
        if (!this.canManage || this.mode === 'view' || !this.canSubmit()) {
            return;
        }

        this.save.emit({
            id: this.workingModel.id ?? null,
            kod: this.workingModel.kod.trim().toUpperCase(),
            ad: this.workingModel.ad.trim(),
            aktifMi: this.workingModel.aktifMi,
            icerikKalemleri: this.workingModel.icerikKalemleri.map((item) => ({
                hizmetKodu: item.hizmetKodu,
                hizmetAdi: this.getHizmetLabel(item.hizmetKodu),
                miktar: item.miktar,
                periyot: item.periyot,
                periyotAdi: this.getPeriyotLabel(item.periyot),
                kullanimTipi: item.kullanimTipi,
                kullanimTipiAdi: this.getKullanimTipiLabel(item.kullanimTipi),
                kullanimNoktasi: item.kullanimNoktasi,
                kullanimNoktasiAdi: this.getKullanimNoktasiLabel(item.kullanimNoktasi),
                kullanimBaslangicSaati: item.kullanimBaslangicSaati?.trim() ? item.kullanimBaslangicSaati.trim() : null,
                kullanimBitisSaati: item.kullanimBitisSaati?.trim() ? item.kullanimBitisSaati.trim() : null,
                checkInGunuGecerliMi: item.checkInGunuGecerliMi,
                checkOutGunuGecerliMi: item.checkOutGunuGecerliMi,
                aciklama: item.aciklama?.trim() ? item.aciklama.trim() : null
            }))
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

    addIcerikKalemi(): void {
        if (this.isReadOnly) {
            return;
        }

        this.workingModel.icerikKalemleri = [
            ...this.workingModel.icerikKalemleri,
            {
                hizmetKodu: '',
                hizmetAdi: '',
                miktar: 1,
                periyot: '',
                periyotAdi: '',
                kullanimTipi: 'Adetli',
                kullanimTipiAdi: 'Adetli',
                kullanimNoktasi: 'Genel',
                kullanimNoktasiAdi: 'Genel',
                kullanimBaslangicSaati: null,
                kullanimBitisSaati: null,
                checkInGunuGecerliMi: true,
                checkOutGunuGecerliMi: true,
                aciklama: null
            }
        ];
    }

    removeIcerikKalemi(index: number): void {
        if (this.isReadOnly) {
            return;
        }

        this.workingModel.icerikKalemleri = this.workingModel.icerikKalemleri.filter((_, itemIndex) => itemIndex !== index);
    }

    syncIcerikAdlari(item: KonaklamaTipiIcerikDto): void {
        item.hizmetAdi = this.getHizmetLabel(item.hizmetKodu);
        item.periyotAdi = this.getPeriyotLabel(item.periyot);
        item.kullanimTipiAdi = this.getKullanimTipiLabel(item.kullanimTipi);
        item.kullanimNoktasiAdi = this.getKullanimNoktasiLabel(item.kullanimNoktasi);
    }

    trackIcerikKalemi(index: number, item: KonaklamaTipiIcerikDto): string {
        return `${index}-${item.hizmetKodu}-${item.periyot}`;
    }

    private cloneModel(model: KonaklamaTipiDto): KonaklamaTipiDto {
        return {
            ...model,
            icerikKalemleri: (model.icerikKalemleri ?? []).map((item) => ({ ...item }))
        };
    }

    private getHizmetLabel(value: string): string {
        return this.hizmetSecenekleri.find((item) => item.value === value)?.label ?? value;
    }

    private getPeriyotLabel(value: string): string {
        return this.periyotSecenekleri.find((item) => item.value === value)?.label ?? value;
    }

    private getKullanimTipiLabel(value: string): string {
        return this.kullanimTipiSecenekleri.find((item) => item.value === value)?.label ?? value;
    }

    private getKullanimNoktasiLabel(value: string): string {
        return this.kullanimNoktasiSecenekleri.find((item) => item.value === value)?.label ?? value;
    }
}

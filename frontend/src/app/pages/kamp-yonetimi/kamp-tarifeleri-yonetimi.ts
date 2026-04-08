import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { AuthService } from '../auth/auth.service';
import { KampKonaklamaTarifeYonetimDto, KampProgramiSecenekDto, KampTarifeKaydetRequestDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-tarifeleri-yonetimi',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        TableModule,
        ButtonModule,
        ToolbarModule,
        SelectModule,
        CheckboxModule,
        ToastModule,
        InputNumberModule,
        InputTextModule
    ],
    template: `
        <p-toast></p-toast>
        <div class="card">
            <p-toolbar class="mb-4">
                <ng-template pTemplate="left">
                    <button pButton pRipple type="button" icon="pi pi-refresh" class="p-button-rounded p-button-text" (click)="onRefresh()" title="Yenile"></button>
                </ng-template>
                <ng-template pTemplate="right">
                    <button
                        pButton
                        pRipple
                        type="button"
                        label="Kaydet"
                        icon="pi pi-check"
                        class="p-button-success mr-2"
                        (click)="onSave()"
                        [disabled]="!canManage || saving">
                    </button>
                </ng-template>
            </p-toolbar>

            <div class="mb-3">
                <label for="program-select" class="mr-2">Kamp Programı</label>
                <p-select
                    id="program-select"
                    [(ngModel)]="selectedProgramId"
                    [options]="programlar"
                    optionLabel="ad"
                    optionValue="id"
                    placeholder="Program seçiniz"
                    (onChange)="onProgramChange()"
                    [disabled]="loading">
                </p-select>
            </div>

            <p-table
                #dt
                [value]="tarifeler"
                [loading]="loading"
                styleClass="kamp-table"
                [tableStyle]="{ 'min-width': '92rem' }"
                responsiveLayout="scroll">
                <ng-template pTemplate="header">
                    <tr>
                        <th style="width:8rem">Kod</th>
                        <th>Ad</th>
                        <th style="width:6rem">Min Kişi</th>
                        <th style="width:6rem">Maks Kişi</th>
                        <th style="width:9rem">Kamu Günlük</th>
                        <th style="width:9rem">Diğer Günlük</th>
                        <th style="width:8rem">Buzdolabı</th>
                        <th style="width:8rem">TV</th>
                        <th style="width:8rem">Klima</th>
                        <th style="width:5rem">Aktif</th>
                        <th style="width:5rem">İşlem</th>
                    </tr>
                </ng-template>
                <ng-template pTemplate="body" let-tarife let-rowIndex="rowIndex">
                    <tr>
                        <td>
                            <input
                                pInputText
                                [(ngModel)]="tarife.kod"
                                [disabled]="!canManage"
                                class="w-full">
                        </td>
                        <td>
                            <input
                                pInputText
                                [(ngModel)]="tarife.ad"
                                [disabled]="!canManage"
                                class="w-full">
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.minimumKisi"
                                [disabled]="!canManage"
                                [useGrouping]="false">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.maksimumKisi"
                                [disabled]="!canManage"
                                [useGrouping]="false">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.kamuGunlukUcret"
                                [disabled]="!canManage"
                                [useGrouping]="false"
                                mode="decimal"
                                [minFractionDigits]="2"
                                [maxFractionDigits]="2">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.digerGunlukUcret"
                                [disabled]="!canManage"
                                [useGrouping]="false"
                                mode="decimal"
                                [minFractionDigits]="2"
                                [maxFractionDigits]="2">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.buzdolabiGunlukUcret"
                                [disabled]="!canManage"
                                [useGrouping]="false"
                                mode="decimal"
                                [minFractionDigits]="2"
                                [maxFractionDigits]="2">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.televizyonGunlukUcret"
                                [disabled]="!canManage"
                                [useGrouping]="false"
                                mode="decimal"
                                [minFractionDigits]="2"
                                [maxFractionDigits]="2">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-inputNumber
                                [(ngModel)]="tarife.klimaGunlukUcret"
                                [disabled]="!canManage"
                                [useGrouping]="false"
                                mode="decimal"
                                [minFractionDigits]="2"
                                [maxFractionDigits]="2">
                            </p-inputNumber>
                        </td>
                        <td>
                            <p-checkbox
                                [(ngModel)]="tarife.aktifMi"
                                [disabled]="!canManage"
                                [binary]="true">
                            </p-checkbox>
                        </td>
                        <td>
                            <button
                                pButton
                                type="button"
                                icon="pi pi-trash"
                                class="p-button-rounded p-button-danger p-button-sm"
                                (click)="onRemoveTarife(rowIndex)"
                                [disabled]="!canManage"
                                title="Sil">
                            </button>
                        </td>
                    </tr>
                </ng-template>
                <ng-template pTemplate="emptymessage">
                    <tr>
                        <td colspan="11" class="text-center py-4">
                            {{ loading ? 'Yükleniyor...' : 'Bu programa ait tarife bulunmamaktadır.' }}
                        </td>
                    </tr>
                </ng-template>
            </p-table>

            <div class="mt-3" *ngIf="canManage && selectedProgramId">
                <button
                    pButton
                    type="button"
                    label="Tarife Ekle"
                    icon="pi pi-plus"
                    class="p-button-info"
                    (click)="onAddTarife()">
                </button>
            </div>
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class KampTarifeleriYonetimiComponent implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    programlar: KampProgramiSecenekDto[] = [];
    tarifeler: KampKonaklamaTarifeYonetimDto[] = [];
    selectedProgramId: number | null = null;
    loading = false;
    saving = false;
    canManage = false;
    canView = false;

    ngOnInit(): void {
        this.checkPermissions();
        this.loadBaglam();
    }

    private checkPermissions(): void {
        this.canManage = this.authService.hasPermission('KampTarifeYonetimi.Manage');
        this.canView = this.authService.hasPermission('KampTarifeYonetimi.View') || this.authService.hasPermission('KampTarifeYonetimi.Menu');
    }

    private loadBaglam(): void {
        this.loading = true;
        this.service.getKampTarifeYonetimBaglam().subscribe({
            next: (baglam) => {
                this.programlar = baglam.programlar;
                if (this.programlar.length > 0) {
                    this.selectedProgramId = this.programlar[0].id;
                    this.loadTarifeler();
                } else {
                    this.loading = false;
                    this.messageService.add({ severity: 'warning', summary: 'Bilgi', detail: 'Aktif kamp programı bulunmamaktadır.' });
                    this.cdr.detectChanges();
                }
            },
            error: (error) => {
                this.loading = false;
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Kamp tarife bağlamı yüklenemedi.' });
                console.error(error);
                this.cdr.detectChanges();
            }
        });
    }

    private loadTarifeler(): void {
        if (!this.selectedProgramId) return;

        this.loading = true;
        this.service.getKampTarifeleri(this.selectedProgramId).subscribe({
            next: (tarifeler) => {
                this.tarifeler = tarifeler;
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (error) => {
                this.loading = false;
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Tarifeler yüklenemedi.' });
                console.error(error);
                this.cdr.detectChanges();
            }
        });
    }

    onProgramChange(): void {
        this.loadTarifeler();
    }

    onAddTarife(): void {
        if (!this.selectedProgramId) return;

        const newTarife: KampKonaklamaTarifeYonetimDto = {
            id: null,
            kampProgramiId: this.selectedProgramId,
            kod: '',
            ad: '',
            minimumKisi: 1,
            maksimumKisi: 10,
            kamuGunlukUcret: 0,
            digerGunlukUcret: 0,
            buzdolabiGunlukUcret: 0,
            televizyonGunlukUcret: 0,
            klimaGunlukUcret: 0,
            aktifMi: true
        };

        this.tarifeler = [newTarife, ...this.tarifeler];
        this.cdr.detectChanges();
    }

    onRemoveTarife(index: number): void {
        this.tarifeler = this.tarifeler.filter((_, i) => i !== index);
        this.cdr.detectChanges();
    }

    onRefresh(): void {
        this.loadTarifeler();
    }

    onSave(): void {
        if (!this.selectedProgramId) return;

        this.saving = true;
        const request: KampTarifeKaydetRequestDto = {
            tarifeler: this.tarifeler
        };

        this.service.kaydetKampTarifeleri(this.selectedProgramId, request).subscribe({
            next: (updatedTarifeler) => {
                this.tarifeler = updatedTarifeler;
                this.saving = false;
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Tarifeler kaydedildi.' });
                this.cdr.detectChanges();
            },
            error: (error) => {
                this.saving = false;
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Tarifeler kaydedilemedi.' });
                console.error(error);
                this.cdr.detectChanges();
            }
        });
    }
}

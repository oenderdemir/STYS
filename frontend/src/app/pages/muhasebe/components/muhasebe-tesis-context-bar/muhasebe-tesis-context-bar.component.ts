import { CommonModule } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import {
    MuhasebeTesisContextService,
    MuhasebeTesisModel
} from '../../services/muhasebe-tesis-context.service';

/**
 * Çalışma Tesisi bağlam çubuğu (Context Bar).
 *
 * Her muhasebe sayfasının üst kısmında gösterilir. Seçili tesisi
 * bir etiket olarak görüntüler ve "Değiştir" butonu sunar.
 * Eğer hiç tesis seçili değilse uyarı görünümü gösterir.
 *
 * Kullanım (her muhasebe sayfasının template'inde, toolbar'dan hemen önce):
 * ```html
 * <app-muhasebe-tesis-context-bar />
 * ```
 */
@Component({
    selector: 'app-muhasebe-tesis-context-bar',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        DialogModule,
        SelectModule,
        TagModule
    ],
    template: `
        <!-- ── Context Bar ── -->
        <div class="muhasebe-tesis-context-bar flex align-items-center justify-content-between px-3 py-2 mb-3 border-round-lg surface-card border-1 border-surface-200 dark:border-surface-700 shadow-1">
            <div class="flex align-items-center gap-2">
                <i class="pi pi-building text-primary text-lg"></i>
                <span class="text-sm text-surface-500 dark:text-surface-400 font-medium">Çalışma Tesisi:</span>

                @if (context.seciliTesis(); as tesis) {
                    <p-tag
                        [value]="tesis.ad"
                        severity="info"
                        styleClass="text-sm font-semibold px-3 py-1" />
                } @else {
                    <p-tag
                        value="Tesisi seçilmedi"
                        severity="warn"
                        icon="pi pi-exclamation-triangle"
                        styleClass="text-sm px-3 py-1" />
                }
            </div>

            <div class="flex align-items-center gap-2">
                @if (!context.seciliTesis()) {
                    <p-button
                        label="Tesisi Seç"
                        icon="pi pi-cog"
                        size="small"
                        severity="warn"
                        (onClick)="degistirDialogAc()" />
                } @else {
                    <p-button
                        label="Değiştir"
                        icon="pi pi-pencil"
                        size="small"
                        severity="secondary"
                        text
                        (onClick)="degistirDialogAc()" />
                }
            </div>
        </div>

        <!-- ── Tesis Değiştirme Dialog'u ── -->
        <p-dialog
            [(visible)]="degistirDialogVisible"
            [modal]="true"
            [draggable]="false"
            header="Çalışma Tesisi Değiştir"
            [style]="{ width: '460px' }">

            <div class="flex flex-col gap-4">
                <div class="text-surface-600 dark:text-surface-400 text-sm leading-relaxed">
                    <i class="pi pi-info-circle mr-2 text-primary"></i>
                    Çalışma tesisini değiştirdiğinizde tüm muhasebe ekranları yeni tesise göre çalışacaktır.
                </div>

                <div class="field">
                    <label class="block text-sm font-medium mb-2">Yeni Çalışma Tesisi</label>
                    <p-select
                        [options]="context.tesisSecenekleri()"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="yeniTesisId"
                        placeholder="Tesisi seçiniz..."
                        appendTo="body"
                        styleClass="w-full"
                        [loading]="context.tesislerLoading()"
                        [filter]="true"
                        filterBy="label" />
                </div>

                <div class="flex justify-end gap-2 pt-2">
                    @if (context.seciliTesis()) {
                        <!-- Mevcut tesis varsa iptal butonu göster -->
                        <p-button
                            label="İptal"
                            icon="pi pi-times"
                            severity="secondary"
                            text
                            (onClick)="degistirDialogVisible = false" />
                    }
                    <p-button
                        label="Kaydet"
                        icon="pi pi-check"
                        [disabled]="!yeniTesisId() || context.tesislerLoading()"
                        (onClick)="onDegistir()" />
                </div>
            </div>
        </p-dialog>
    `,
    styles: [`
        :host {
            display: block;
        }
        .muhasebe-tesis-context-bar {
            transition: all 0.2s ease;
        }
    `]
})
export class MuhasebeTesisContextBarComponent {
    readonly context = inject(MuhasebeTesisContextService);

    // ── State ──

    degistirDialogVisible = false;
    yeniTesisId = signal<number | null>(null);

    // ── Efekt: dialog açıldığında mevcut seçimi dropdown'a yaz ──

    private readonly _syncDialogEffect = effect(() => {
        // Dialog her açıldığında mevcut tesis id'sini göster
        if (this.degistirDialogVisible) {
            const mevcut = this.context.seciliTesis();
            this.yeniTesisId.set(mevcut?.id ?? null);
        }
    });

    // ── Actions ──

    degistirDialogAc(): void {
        this.yeniTesisId.set(this.context.seciliTesis()?.id ?? null);
        this.degistirDialogVisible = true;
    }

    onDegistir(): void {
        const id = this.yeniTesisId();
        if (!id) return;

        const tesisler = this.context.tesisler();
        const secilen = tesisler.find(t => t.id === id);
        if (secilen) {
            this.context.selectTesis(secilen);
            this.degistirDialogVisible = false;
        }
    }
}

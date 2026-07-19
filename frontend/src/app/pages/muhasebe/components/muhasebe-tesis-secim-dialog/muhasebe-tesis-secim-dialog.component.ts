import { CommonModule } from '@angular/common';
import { Component, effect, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { MuhasebeTesisContextService, MuhasebeTesisModel } from '../../services/muhasebe-tesis-context.service';

/**
 * Zorunlu tesis seçim dialog'u.
 *
 * Kullanıcı muhasebe modülüne girdiğinde seçili çalışma tesisi yoksa
 * bu dialog otomatik açılır. Kullanıcı bir tesis seçmeden dialog'u
 * kapatamaz — bu sayede tüm muhasebe ekranları her zaman geçerli bir
 * tesis bağlamında çalışır.
 *
 * Kullanım (her muhasebe sayfasının template'inde):
 * ```html
 * <app-muhasebe-tesis-secim-dialog />
 * ```
 *
 * Dialog otomatik olarak context'teki seciliTesis sinyalini izler:
 * - seciliTesis null → dialog görünür, kapatılamaz
 * - seciliTesis dolu → dialog gizlenir
 */
@Component({
    selector: 'app-muhasebe-tesis-secim-dialog',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, DialogModule, SelectModule],
    changeDetection: ChangeDetectionStrategy.Eager,
    template: `
        <p-dialog [(visible)]="dialogVisible" [modal]="true" [closable]="false" [draggable]="false" header="Çalışma Tesisi Seçimi" styleClass="tesis-secim-dialog" [style]="{ width: '460px' }">
            <div class="flex flex-col gap-4">
                <div class="text-surface-600 dark:text-surface-400 text-sm leading-relaxed">
                    <i class="pi pi-info-circle mr-2 text-primary"></i>
                    Muhasebe işlemlerine devam edebilmek için lütfen çalışacağınız tesisi seçiniz. Bu seçim sayfa yenilense dahi hatırlanacaktır.
                </div>

                <div class="field">
                    <label class="block text-sm font-medium mb-2">Çalışma Tesisi</label>
                    <p-select
                        [options]="tesisSecenekleri()"
                        optionLabel="label"
                        optionValue="value"
                        [(ngModel)]="secilenTesisId"
                        placeholder="Tesis seçiniz..."
                        appendTo="body"
                        styleClass="w-full"
                        [loading]="tesislerLoading()"
                        [filter]="true"
                        filterBy="label"
                        (onChange)="onTesisSecildi()"
                    />
                </div>

                @if (tesislerError()) {
                    <div class="flex align-items-center gap-2 p-3 bg-red-50 dark:bg-red-900/20 border-round text-red-700 dark:text-red-400 text-sm">
                        <i class="pi pi-exclamation-triangle"></i>
                        <span>{{ tesislerError() }}</span>
                    </div>
                }

                <div class="flex justify-end pt-2">
                    <p-button label="Tesisi Seç ve Devam Et" icon="pi pi-check" [disabled]="!secilenTesisId() || tesislerLoading()" [loading]="tesislerLoading()" (onClick)="onKaydet()" />
                </div>
            </div>
        </p-dialog>
    `
})
export class MuhasebeTesisSecimDialogComponent {
    private readonly context = inject(MuhasebeTesisContextService);

    // ── State ──

    dialogVisible = true;
    secilenTesisId = signal<number | null>(null);

    // Context'ten gelen reaktif değerler
    tesisSecenekleri = this.context.tesisSecenekleri;
    tesislerLoading = this.context.tesislerLoading;
    tesislerError = this.context.tesislerError;

    // ── Efekt: secili tesis varsa dialog'u kapat ──

    private readonly _autoCloseEffect = effect(() => {
        const tesis = this.context.seciliTesis();
        if (tesis) {
            this.dialogVisible = false;
        } else {
            this.dialogVisible = true;
        }
    });

    // ── Efekt: tesisler yüklendiğinde mevcut seçimi senkronla ──

    private readonly _syncSelectionEffect = effect(() => {
        const tesisler = this.context.tesisler();
        const secili = this.context.seciliTesis();
        if (secili) {
            this.secilenTesisId.set(secili.id);
        } else {
            // Tek tesis varsa otomatik seç
            if (tesisler.length === 1) {
                this.secilenTesisId.set(tesisler[0].id);
            }
        }
    });

    // ── Actions ──

    /** Kullanıcı dropdown'dan bir tesis seçtiğinde tetiklenir. */
    onTesisSecildi(): void {
        // Dropdown değişince selectedTesisId zaten güncellendi — ek işlem yok
    }

    /** "Tesisi Seç ve Devam Et" butonu tıklandığında. */
    onKaydet(): void {
        const id = this.secilenTesisId();
        if (!id) return;

        const tesisler = this.context.tesisler();
        const secilen = tesisler.find((t) => t.id === id);
        if (secilen) {
            this.context.selectTesis(secilen);
        }
    }
}

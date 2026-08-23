import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { CurrentStokMaliyetPolitikasiModel, STOK_MALIYET_YONTEMI_SECENEKLERI } from '../../stok-hareketleri/stok-hareketleri.dto';

@Component({
    selector: 'app-stok-maliyet-politikasi-dialog',
    standalone: true,
    imports: [CommonModule, ButtonModule, DialogModule],
    templateUrl: './stok-maliyet-politikasi-dialog.component.html'
})
export class StokMaliyetPolitikasiDialogComponent {
    @Input() visible = false;
    @Input() currentPolitika: CurrentStokMaliyetPolitikasiModel | null = null;
    @Input() secilenMaliyetYontemi = 'AgirlikliOrtalama';
    @Input() saving = false;
    @Output() secilenMaliyetYontemiChange = new EventEmitter<string>();
    @Output() save = new EventEmitter<void>();

    readonly maliyetYontemiSecenekleri = STOK_MALIYET_YONTEMI_SECENEKLERI;

    selectMaliyetYontemi(value: string): void {
        this.secilenMaliyetYontemiChange.emit(value);
    }

    savePolitika(): void {
        this.save.emit();
    }
}

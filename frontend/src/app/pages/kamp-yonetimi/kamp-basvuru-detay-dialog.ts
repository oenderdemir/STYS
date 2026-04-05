import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { tryReadApiMessage } from '../../core/api';
import { KampBasvuruDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-basvuru-detay-dialog',
    standalone: true,
    imports: [CommonModule, ButtonModule, DialogModule, TableModule, TagModule, ToastModule],
    templateUrl: './kamp-basvuru-detay-dialog.html',
    styleUrl: './kamp-basvuru-detay-dialog.scss',
    providers: [MessageService]
})
export class KampBasvuruDetayDialogComponent implements OnChanges {
    private readonly service = inject(KampYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    @Input() visible = false;
    @Input() basvuruId: number | null = null;
    @Input() canManageParticipants = false;

    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() degisti = new EventEmitter<void>();

    detay: KampBasvuruDto | null = null;
    loading = false;
    iptalEdilenKatilimciId: number | null = null;

    ngOnChanges(changes: SimpleChanges): void {
        if ((changes['visible'] || changes['basvuruId']) && this.visible && this.basvuruId) {
            this.loadDetail(this.basvuruId);
        }

        if (changes['visible'] && !this.visible) {
            this.detay = null;
            this.loading = false;
            this.iptalEdilenKatilimciId = null;
        }
    }

    close(): void {
        this.visibleChange.emit(false);
    }

    durumSeverity(durum: string): 'success' | 'danger' | 'secondary' | 'warn' | 'info' | 'contrast' {
        switch (durum) {
            case 'TahsisEdildi':
                return 'success';
            case 'TahsisEdilemedi':
            case 'Reddedildi':
                return 'danger';
            case 'IptalEdildi':
                return 'secondary';
            case 'Beklemede':
                return 'warn';
            default:
                return 'info';
        }
    }

    katilimciIptalEt(katilimciId: number | null | undefined): void {
        if (!this.detay || !katilimciId || !this.canManageParticipants || this.iptalEdilenKatilimciId !== null) {
            return;
        }

        this.iptalEdilenKatilimciId = katilimciId;
        this.service.katilimciIptalEt(this.detay.id, katilimciId)
            .pipe(finalize(() => {
                this.iptalEdilenKatilimciId = null;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    if (result.uyariMesaji) {
                        this.messageService.add({ severity: 'warn', summary: 'Uyari', detail: result.uyariMesaji });
                    } else {
                        this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Katilimci basvurudan cikarildi.' });
                    }

                    this.degisti.emit();
                    this.loadDetail(this.detay!.id);
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadDetail(id: number): void {
        this.loading = true;
        this.service.getKampBasvuruById(id)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (detay) => {
                    this.detay = detay;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.detay = null;
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}

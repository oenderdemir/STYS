import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { KampBasvuruDetayDialogComponent } from './kamp-basvuru-detay-dialog';
import { KampRezervasyonBaglamDto, KampRezervasyonListeDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-rezervasyonlari',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, DialogModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule, KampBasvuruDetayDialogComponent],
    templateUrl: './kamp-rezervasyonlari.html',
    styleUrl: './kamp-rezervasyonlari.scss',
    providers: [MessageService]
})
export class KampRezervasyonlariPage implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    baglam: KampRezervasyonBaglamDto | null = null;
    kayitlar: KampRezervasyonListeDto[] = [];
    loading = false;
    selectedKampDonemiId = 0;
    selectedTesisId = 0;
    selectedDurum = '';

    iptalDialogVisible = false;
    iptalYapiliyorId: number | null = null;
    iptalNedeni = '';
    iptalKaydediliyor = false;
    detayDialogVisible = false;
    selectedBasvuruId: number | null = null;

    get canManage(): boolean {
        return this.authService.hasPermission('KampRezervasyonYonetimi.Manage');
    }

    ngOnInit(): void {
        this.loadBaglam();
    }

    refresh(): void {
        this.loadListe();
    }

    onFiltersChanged(): void {
        this.loadListe();
    }

    acDetay(item: KampRezervasyonListeDto): void {
        this.selectedBasvuruId = item.kampBasvuruId;
        this.detayDialogVisible = true;
    }

    onDetayDegisti(): void {
        this.loadListe();
    }

    acIptalDialog(item: KampRezervasyonListeDto): void {
        if (!this.canManage) {
            return;
        }

        this.iptalYapiliyorId = item.id;
        this.iptalNedeni = '';
        this.iptalDialogVisible = true;
    }

    iptalEt(): void {
        if (!this.iptalYapiliyorId || !this.canManage) {
            return;
        }

        const id = this.iptalYapiliyorId;
        this.iptalKaydediliyor = true;
        this.service.iptalEtKampRezervasyon(id, { iptalNedeni: this.iptalNedeni })
            .pipe(finalize(() => {
                this.iptalKaydediliyor = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.iptalDialogVisible = false;
                    this.iptalYapiliyorId = null;
                    this.iptalNedeni = '';
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Rezervasyon iptal edildi.' });
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    durumSeverity(durum: string): 'success' | 'danger' | 'secondary' | 'warn' | 'info' | 'contrast' {
        switch (durum) {
            case 'Aktif':
                return 'success';
            case 'IptalEdildi':
                return 'danger';
            default:
                return 'secondary';
        }
    }

    private loadBaglam(): void {
        this.loading = true;
        this.service.getKampRezervasyonBaglam()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.baglam = baglam;
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private loadListe(): void {
        this.loading = true;
        this.service.getKampRezervasyonlari(this.selectedKampDonemiId, this.selectedTesisId, this.selectedDurum)
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.kayitlar = items;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.kayitlar = [];
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

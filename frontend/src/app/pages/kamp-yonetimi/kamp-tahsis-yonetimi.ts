import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { AuthService } from '../auth';
import { KampBasvuruDetayDialogComponent } from './kamp-basvuru-detay-dialog';
import { KampNoShowIptalSonucDto, KampTahsisBaglamDto, KampTahsisListeDto, KampTahsisOtomatikKararSonucDto, KampRezervasyonUretSonucDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-tahsis-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink, ButtonModule, TableModule, TagModule, ToastModule, ToolbarModule, KampBasvuruDetayDialogComponent],
    templateUrl: './kamp-tahsis-yonetimi.html',
    styleUrl: './kamp-tahsis-yonetimi.scss',
    providers: [MessageService]
})
export class KampTahsisYonetimiPage implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    baglam: KampTahsisBaglamDto | null = null;
    kayitlar: KampTahsisListeDto[] = [];
    loading = false;
    kararKaydediliyorId: number | null = null;
    otomatikTahsisCalisiyor = false;
    noShowIptalCalisiyor = false;
    rezervasyonUretiliyorId: number | null = null;
    detayDialogVisible = false;
    selectedBasvuruId: number | null = null;
    selectedKampDonemiId = 0;
    selectedTesisId = 0;
    selectedDurum = '';

    get canManage(): boolean {
        return this.authService.hasPermission('KampTahsisYonetimi.Manage');
    }

    get canRunOtomatikTahsis(): boolean {
        return this.canManage && this.selectedKampDonemiId > 0 && this.selectedTesisId > 0 && !this.otomatikTahsisCalisiyor && this.kararKaydediliyorId === null;
    }

    get canRunNoShowIptal(): boolean {
        return this.canManage && this.selectedKampDonemiId > 0 && !this.noShowIptalCalisiyor && this.kararKaydediliyorId === null;
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

    acDetay(item: KampTahsisListeDto): void {
        this.selectedBasvuruId = item.id;
        this.detayDialogVisible = true;
    }

    onDetayDegisti(): void {
        this.loadListe();
    }

    uygulaOtomatikTahsis(): void {
        if (!this.canManage) {
            return;
        }

        if (this.selectedKampDonemiId <= 0 || this.selectedTesisId <= 0) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Filtre',
                detail: 'Otomatik tahsis icin once kamp donemi ve tesis secilmelidir.'
            });
            return;
        }

        this.otomatikTahsisCalisiyor = true;
        this.service.uygulaOtomatikKampTahsisi({
            kampDonemiId: this.selectedKampDonemiId,
            tesisId: this.selectedTesisId
        })
            .pipe(finalize(() => {
                this.otomatikTahsisCalisiyor = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: this.buildOtomatikTahsisMessage(result)
                    });
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    kararVer(item: KampTahsisListeDto, durum: string): void {
        if (this.kararKaydediliyorId !== null || !this.canManage || item.durum === durum) {
            return;
        }

        this.kararKaydediliyorId = item.id;
        this.service.kararVerKampTahsisi(item.id, { durum })
            .pipe(finalize(() => {
                this.kararKaydediliyorId = null;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Basarili', detail: 'Tahsis karari kaydedildi.' });
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    durumSeverity(durum: string): 'success' | 'danger' | 'warn' | 'info' | 'secondary' | 'contrast' {
        switch (durum) {
            case 'TahsisEdildi':
                return 'success';
            case 'TahsisEdilemedi':
            case 'Reddedildi':
                return 'danger';
            case 'IptalEdildi':
                return 'secondary';
            default:
                return 'warn';
        }
    }

    private loadBaglam(): void {
        this.loading = true;
        this.service.getKampTahsisBaglam()
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
        this.service.getKampTahsisleri(this.selectedKampDonemiId, this.selectedTesisId, this.selectedDurum)
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

    uretRezervasyon(item: KampTahsisListeDto): void {
        if (!this.canManage || this.rezervasyonUretiliyorId !== null) {
            return;
        }

        this.rezervasyonUretiliyorId = item.id;
        this.service.uretKampRezervasyon(item.id)
            .pipe(finalize(() => {
                this.rezervasyonUretiliyorId = null;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result: KampRezervasyonUretSonucDto) => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Rezervasyon Uretildi',
                        detail: `${result.rezervasyonNo} numarali rezervasyon olusturuldu.`
                    });
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    uygulaNoShowIptal(): void {
        if (!this.canRunNoShowIptal) {
            return;
        }

        this.noShowIptalCalisiyor = true;
        this.service.noShowIptalUygula(this.selectedKampDonemiId)
            .pipe(finalize(() => {
                this.noShowIptalCalisiyor = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result: KampNoShowIptalSonucDto) => {
                    this.messageService.add({
                        severity: 'success',
                        summary: 'Basarili',
                        detail: `${result.degerlendirilenBasvuruSayisi} basvuru degerlendirildi, ${result.iptalEdilenSayisi} no-show basvuru iptal edildi.`
                    });
                    this.loadListe();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    private buildOtomatikTahsisMessage(result: KampTahsisOtomatikKararSonucDto): string {
        return `${result.tahsisEdilenSayisi} basvuru tahsis edildi, ${result.tahsisEdilemeyenSayisi} basvuru tahsis edilemedi. ${result.guncellenenKayitSayisi} kayit guncellendi.`;
    }
}

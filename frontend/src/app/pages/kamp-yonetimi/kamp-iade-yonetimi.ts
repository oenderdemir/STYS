import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { KampBasvuruDto, KampIadeKarariDto, KampTahsisBaglamDto, KampTahsisListeDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-iade-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, TableModule, TagModule, ToolbarModule],
    templateUrl: './kamp-iade-yonetimi.html',
    styleUrl: './kamp-iade-yonetimi.scss'
})
export class KampIadeYonetimiPage implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly cdr = inject(ChangeDetectorRef);

    baglam: KampTahsisBaglamDto | null = null;
    kayitlar: KampTahsisListeDto[] = [];
    selectedBasvuru: KampBasvuruDto | null = null;
    sonuc: KampIadeKarariDto | null = null;

    loading = false;
    detayLoading = false;
    hesapliyor = false;
    hataMesaji: string | null = null;

    selectedKampDonemiId = 0;
    selectedTesisId = 0;
    selectedDurum = '';

    vazgecmeTarihi = '';
    odenenToplamTutar = 0;
    mazeretliZorunluAyrilisMi = false;
    kullanilmayanGunSayisi = 0;

    ngOnInit(): void {
        this.loadBaglam();
    }

    get toplamGunSayisi(): number {
        if (!this.selectedBasvuru?.konaklamaBaslangicTarihi || !this.selectedBasvuru?.konaklamaBitisTarihi) {
            return 0;
        }

        const baslangic = new Date(this.selectedBasvuru.konaklamaBaslangicTarihi);
        const bitis = new Date(this.selectedBasvuru.konaklamaBitisTarihi);
        const diff = bitis.getTime() - baslangic.getTime();
        return Math.max(0, Math.floor(diff / (1000 * 60 * 60 * 24)) + 1);
    }

    refresh(): void {
        this.loadListe();
    }

    onFiltersChanged(): void {
        this.loadListe();
    }

    secBasvuru(item: KampTahsisListeDto): void {
        this.detayLoading = true;
        this.sonuc = null;
        this.hataMesaji = null;

        this.service.getKampBasvuruById(item.id)
            .pipe(finalize(() => {
                this.detayLoading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (detay) => {
                    this.selectedBasvuru = detay;
                    this.vazgecmeTarihi = new Date().toISOString().slice(0, 10);
                    this.odenenToplamTutar = detay.avansToplamTutar;
                    this.kullanilmayanGunSayisi = 0;
                    this.mazeretliZorunluAyrilisMi = false;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.selectedBasvuru = null;
                    this.hataMesaji = this.resolveErrorMessage(error);
                    this.cdr.detectChanges();
                }
            });
    }

    hesapla(): void {
        if (!this.selectedBasvuru || this.hesapliyor) {
            return;
        }

        this.hesapliyor = true;
        this.sonuc = null;

        this.service.hesaplaIadeKarari({
            basvuruDurumu: this.selectedBasvuru.durum,
            kampDonemiId: this.selectedBasvuru.kampDonemiId,
            kampBaslangicTarihi: this.selectedBasvuru.konaklamaBaslangicTarihi,
            toplamGunSayisi: this.toplamGunSayisi,
            vazgecmeTarihi: this.vazgecmeTarihi || null,
            avansTutari: this.selectedBasvuru.avansToplamTutar,
            donemToplamTutari: this.selectedBasvuru.donemToplamTutar,
            odenenToplamTutar: this.odenenToplamTutar,
            mazeretliZorunluAyrilisMi: this.mazeretliZorunluAyrilisMi,
            kullanilmayanGunSayisi: Math.max(0, this.kullanilmayanGunSayisi)
        })
            .pipe(finalize(() => {
                this.hesapliyor = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (result) => {
                    this.sonuc = result;
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.hataMesaji = this.resolveErrorMessage(error);
                    this.cdr.detectChanges();
                }
            });
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
                    this.hataMesaji = this.resolveErrorMessage(error);
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
                    this.hataMesaji = this.resolveErrorMessage(error);
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

import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { KampProgramiSecenekDto, KampPuanBasvuruSahibiTipiDto, KampPuanBasvuruSahibiTipSecenekDto, KampPuanKuralSetiDto, KampSecenekDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';
import { AuthService } from '../auth';

@Component({
    selector: 'app-kamp-puan-kurali-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, TableModule, ToastModule, ToolbarModule],
    templateUrl: './kamp-puan-kurali-yonetimi.html',
    styleUrl: './kamp-puan-kurali-yonetimi.scss',
    providers: [MessageService]
})
export class KampPuanKuraliYonetimiPage implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly messageService = inject(MessageService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    programlar: KampProgramiSecenekDto[] = [];
    globalBasvuruSahibiTipleri: KampPuanBasvuruSahibiTipSecenekDto[] = [];
    kuralSetleri: KampPuanKuralSetiDto[] = [];
    basvuruSahibiTipleri: KampPuanBasvuruSahibiTipiDto[] = [];
    katilimciTipleri: KampSecenekDto[] = [];

    get canView(): boolean {
        return this.hasAnyPermission('KampPuanKuraliYonetimi.View', 'KampPuanKuraliYonetimi.Menu');
    }

    get canManage(): boolean {
        return this.hasAnyPermission('KampPuanKuraliYonetimi.Manage');
    }

    ngOnInit(): void {
        this.load();
    }

    load(): void {
        this.loading = true;
        this.service
            .getKampPuanKuraliYonetimBaglam()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.programlar = [...baglam.programlar].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.globalBasvuruSahibiTipleri = [...baglam.globalBasvuruSahibiTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.kuralSetleri = [...baglam.kuralSetleri].sort((a, b) => {
                        const adA = a.kampProgramiAd ?? '';
                        const adB = b.kampProgramiAd ?? '';
                        if (adA !== adB) {
                            return adA.localeCompare(adB);
                        }

                        return b.kampYili - a.kampYili;
                    });
                    this.basvuruSahibiTipleri = [...baglam.basvuruSahibiTipleri].sort((a, b) => a.oncelikSirasi - b.oncelikSirasi);
                    this.katilimciTipleri = [...baglam.katilimciTipleri];
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    addKuralSeti(): void {
        const maxYil = this.kuralSetleri.length > 0 ? Math.max(...this.kuralSetleri.map((x) => x.kampYili)) : new Date().getFullYear();
        const defaultProgramId = this.programlar[0]?.id ?? 0;
        this.kuralSetleri = [
            {
                kampProgramiId: defaultProgramId,
                kampYili: maxYil + 1,
                oncekiYilSayisi: 2,
                katilimCezaPuani: 20,
                katilimciBasinaPuan: 10,
                aktifMi: true
            },
            ...this.kuralSetleri
        ];
    }

    addBasvuruSahibiTipi(): void {
        const maxOncelik = this.basvuruSahibiTipleri.length > 0 ? Math.max(...this.basvuruSahibiTipleri.map((x) => x.oncelikSirasi)) : 0;
        const defaultProgramId = this.programlar[0]?.id ?? 0;
        const defaultTip = this.globalBasvuruSahibiTipleri[0];
        this.basvuruSahibiTipleri = [
            ...this.basvuruSahibiTipleri,
            {
                kampProgramiId: defaultProgramId,
                kampBasvuruSahibiTipiId: defaultTip?.id ?? 0,
                kod: defaultTip?.kod ?? '',
                ad: defaultTip?.ad ?? '',
                oncelikSirasi: maxOncelik + 1,
                tabanPuan: 0,
                hizmetYiliPuaniAktifMi: true,
                emekliBonusPuani: 0,
                varsayilanKatilimciTipiKodu: this.katilimciTipleri[0]?.kod ?? null,
                aktifMi: true
            }
        ];
    }

    removeKuralSeti(index: number): void {
        this.kuralSetleri = this.kuralSetleri.filter((_, i) => i !== index);
    }

    removeBasvuruSahibiTipi(index: number): void {
        this.basvuruSahibiTipleri = this.basvuruSahibiTipleri.filter((_, i) => i !== index);
    }

    save(): void {
        if (this.saving || !this.canManage) {
            return;
        }

        this.saving = true;
        this.service
            .kaydetKampPuanKuraliYonetimBaglam({
                kuralSetleri: this.kuralSetleri,
                basvuruSahibiTipleri: this.basvuruSahibiTipleri
            })
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.programlar = [...baglam.programlar].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.globalBasvuruSahibiTipleri = [...baglam.globalBasvuruSahibiTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.kuralSetleri = [...baglam.kuralSetleri].sort((a, b) => {
                        const adA = a.kampProgramiAd ?? '';
                        const adB = b.kampProgramiAd ?? '';
                        if (adA !== adB) {
                            return adA.localeCompare(adB);
                        }

                        return b.kampYili - a.kampYili;
                    });
                    this.basvuruSahibiTipleri = [...baglam.basvuruSahibiTipleri].sort((a, b) => a.oncelikSirasi - b.oncelikSirasi);
                    this.katilimciTipleri = [...baglam.katilimciTipleri];
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kamp puan kurallari kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    onBasvuruSahibiTipiChange(item: KampPuanBasvuruSahibiTipiDto): void {
        const selected = this.globalBasvuruSahibiTipleri.find((x) => x.id === item.kampBasvuruSahibiTipiId);
        item.kod = selected?.kod ?? '';
        item.ad = selected?.ad ?? '';
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

    private hasAnyPermission(...permissions: string[]): boolean {
        return permissions.some((permission) => this.authService.hasPermission(permission));
    }
}

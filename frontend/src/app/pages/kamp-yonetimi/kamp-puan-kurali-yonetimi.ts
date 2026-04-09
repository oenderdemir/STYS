import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { tryReadApiMessage } from '../../core/api';
import { UiSeverity } from '../../core/ui/ui-severity.constants';
import { KampProgramiParametreAyariDto, KampProgramiSecenekDto, KampPuanBasvuruSahibiTipiDto, KampPuanBasvuruSahibiTipSecenekDto, KampPuanKuralSetiDto, KampSecenekDto, KampYasUcretKuraliDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';
import { AuthService } from '../auth';

@Component({
    selector: 'app-kamp-puan-kurali-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SelectModule, TableModule, ToastModule, ToolbarModule],
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
    selectedProgramId: number | null = null;
    programlar: KampProgramiSecenekDto[] = [];
    globalBasvuruSahibiTipleri: KampPuanBasvuruSahibiTipSecenekDto[] = [];
    programParametreAyarlari: KampProgramiParametreAyariDto[] = [];
    kuralSetleri: KampPuanKuralSetiDto[] = [];
    basvuruSahibiTipleri: KampPuanBasvuruSahibiTipiDto[] = [];
    katilimciTipleri: KampSecenekDto[] = [];
    yasUcretKurali: KampYasUcretKuraliDto = {
        ucretsizCocukMaxYas: 2,
        yarimUcretliCocukMaxYas: 6,
        yemekOrani: 0.5,
        aktifMi: true
    };

    get filteredProgramParametreAyarlari(): KampProgramiParametreAyariDto[] {
        if (!this.selectedProgramId) return [];
        return this.programParametreAyarlari.filter(x => x.kampProgramiId === this.selectedProgramId);
    }

    get filteredKuralSetleri(): KampPuanKuralSetiDto[] {
        if (!this.selectedProgramId) return [];
        return this.kuralSetleri.filter(x => x.kampProgramiId === this.selectedProgramId).sort((a, b) => b.kampYili - a.kampYili);
    }

    get filteredBasvuruSahibiTipleri(): KampPuanBasvuruSahibiTipiDto[] {
        if (!this.selectedProgramId) return [];
        return this.basvuruSahibiTipleri.filter(x => x.kampProgramiId === this.selectedProgramId).sort((a, b) => a.oncelikSirasi - b.oncelikSirasi);
    }

    get globalBasvuruSahibiTipiOptions(): Array<{ id: number; label: string }> {
        return this.globalBasvuruSahibiTipleri.map((x) => ({
            id: x.id,
            label: `${x.ad} (${x.kod})`
        }));
    }

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
                    if (this.programlar.length > 0 && !this.selectedProgramId) {
                        this.selectedProgramId = this.programlar[0].id;
                    }
                    this.globalBasvuruSahibiTipleri = [...baglam.globalBasvuruSahibiTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.programParametreAyarlari = [...baglam.programParametreAyarlari];
                    this.kuralSetleri = [...baglam.kuralSetleri];
                    this.basvuruSahibiTipleri = [...baglam.basvuruSahibiTipleri];
                    this.katilimciTipleri = [...baglam.katilimciTipleri];
                    this.yasUcretKurali = { ...baglam.yasUcretKurali };
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.detectChanges();
                }
            });
    }

    addKuralSeti(): void {
        if (!this.selectedProgramId) return;
        const programKuralSetleri = this.kuralSetleri.filter(x => x.kampProgramiId === this.selectedProgramId);
        const maxYil = programKuralSetleri.length > 0 ? Math.max(...programKuralSetleri.map((x) => x.kampYili)) : new Date().getFullYear();
        this.kuralSetleri = [
            {
                kampProgramiId: this.selectedProgramId,
                kampYili: maxYil + 1,
                oncekiYilSayisi: 2,
                katilimCezaPuani: 20,
                katilimciBasinaPuan: 10,
                aktifMi: true
            },
            ...this.kuralSetleri
        ];
    }

    addProgramParametreAyari(): void {
        if (!this.selectedProgramId) return;
        // Check if parametre already exists for this program
        if (this.programParametreAyarlari.some(x => x.kampProgramiId === this.selectedProgramId)) {
            this.messageService.add({ severity: 'warning', summary: 'Uyarı', detail: 'Bu program için zaten bir parametre bulunmaktadır.' });
            return;
        }
        this.programParametreAyarlari = [
            ...this.programParametreAyarlari,
            {
                kampProgramiId: this.selectedProgramId,
                kamuAvansKisiBasi: 1700,
                digerAvansKisiBasi: 2550,
                vazgecmeIadeGunSayisi: 7,
                gecBildirimGunlukKesintiyUzdesi: 0.05,
                noShowSuresiGun: 2,
                aktifMi: true
            }
        ];
    }

    addBasvuruSahibiTipi(): void {
        if (!this.selectedProgramId) return;
        const programTipleri = this.basvuruSahibiTipleri.filter(x => x.kampProgramiId === this.selectedProgramId);
        const maxOncelik = programTipleri.length > 0 ? Math.max(...programTipleri.map((x) => x.oncelikSirasi)) : 0;
        const defaultTip = this.globalBasvuruSahibiTipleri[0];
        this.basvuruSahibiTipleri = [
            ...this.basvuruSahibiTipleri,
            {
                kampProgramiId: this.selectedProgramId,
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

    removeProgramParametreAyari(index: number): void {
        this.programParametreAyarlari = this.programParametreAyarlari.filter((_, i) => i !== index);
    }

    removeProgramParametreAyariByItem(item: KampProgramiParametreAyariDto): void {
        this.programParametreAyarlari = this.programParametreAyarlari.filter(x => x.kampProgramiId !== item.kampProgramiId || x.kamuAvansKisiBasi !== item.kamuAvansKisiBasi);
    }

    removeKuralSetiByItem(item: KampPuanKuralSetiDto): void {
        this.kuralSetleri = this.kuralSetleri.filter(x => !(x.kampProgramiId === item.kampProgramiId && x.kampYili === item.kampYili));
    }

    removeBasvuruSahibiTipi(index: number): void {
        this.basvuruSahibiTipleri = this.basvuruSahibiTipleri.filter((_, i) => i !== index);
    }

    removeBasvuruSahibiTipiByItem(item: KampPuanBasvuruSahibiTipiDto): void {
        this.basvuruSahibiTipleri = this.basvuruSahibiTipleri.filter(x => !(x.kampProgramiId === item.kampProgramiId && x.kampBasvuruSahibiTipiId === item.kampBasvuruSahibiTipiId));
    }


    save(): void {
        if (this.saving || !this.canManage) {
            return;
        }

        this.saving = true;
        this.service
            .kaydetKampPuanKuraliYonetimBaglam({
                kuralSetleri: this.kuralSetleri,
                basvuruSahibiTipleri: this.basvuruSahibiTipleri,
                programParametreAyarlari: this.programParametreAyarlari,
                yasUcretKurali: this.yasUcretKurali
            })
            .pipe(finalize(() => {
                this.saving = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (baglam) => {
                    this.programlar = [...baglam.programlar].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.globalBasvuruSahibiTipleri = [...baglam.globalBasvuruSahibiTipleri].sort((a, b) => a.ad.localeCompare(b.ad));
                    this.programParametreAyarlari = [...baglam.programParametreAyarlari];
                    this.kuralSetleri = [...baglam.kuralSetleri];
                    this.basvuruSahibiTipleri = [...baglam.basvuruSahibiTipleri];
                    this.katilimciTipleri = [...baglam.katilimciTipleri];
                    this.yasUcretKurali = { ...baglam.yasUcretKurali };
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
